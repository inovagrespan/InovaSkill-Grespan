using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Analytics;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ImportDbContext dbContext) : ControllerBase
{
    private const int TopProductsLimit = 50;

    [HttpGet("{customerId}/commercial-health")]
    public async Task<ActionResult<CustomerCommercialHealthReport>> GetCommercialHealth(
        [FromRoute] string customerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var linkedCompany = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var rows = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .OrderBy(x => x.TransactionDate)
            .Select(x => new CustomerCommercialHealthTransaction(
                x.CustomerCode,
                x.CustomerName,
                x.City,
                linkedCompany,
                x.DocumentNumber,
                x.ProductCode,
                x.ProductDescription,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg,
                x.TransactionDate))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return NotFound("Cliente não possui transações para análise comercial.");
        }

        return Ok(CustomerCommercialHealthAnalyzer.Build(rows, DateTime.UtcNow));
    }

    [HttpGet("{customerId}/summary")]
    public async Task<ActionResult<CustomerSummaryResponseDto>> GetSummary(
        [FromRoute] string customerId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 365);
        var allCustomerRows = await LoadCustomerMetricTransactions(resolvedCustomerCode, cancellationToken);
        if (allCustomerRows.Count == 0)
        {
            return NotFound("Cliente não possui transações para análise.");
        }

        var baseRow = allCustomerRows[^1];
        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .Select(x => new { x.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var referenceDate = period.CurrentTo.AddTicks(-1);
        var latestWeekStart = CustomerCalculators.StartOfWeekUtc(referenceDate);
        var currentWeekRevenue = (decimal)await dbContext.CustomerSummariesWeekly
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate == latestWeekStart)
            .SumAsync(x => (double)x.Revenue, cancellationToken);
        var previousWeekRevenue = (decimal)await dbContext.CustomerSummariesWeekly
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate == latestWeekStart.AddDays(-7))
            .SumAsync(x => (double)x.Revenue, cancellationToken);

        var metrics = CustomerCalculators.BuildCustomerSummary(
            allCustomerRows,
            period.CurrentFrom,
            period.CurrentTo,
            currentWeekRevenue,
            previousWeekRevenue,
            referenceDate);

        return Ok(new CustomerSummaryResponseDto(
            resolvedCustomerCode,
            baseRow.CustomerName,
            baseRow.City,
            customer?.Name ?? baseRow.CustomerName,
            metrics.LastPurchaseDate,
            metrics.Status,
            metrics.TotalRevenue,
            metrics.AverageTicket,
            metrics.AverageRevenueMonthly,
            metrics.AverageRevenueWeekly,
            metrics.TotalQuantity,
            metrics.TotalWeight,
            metrics.TotalOrders,
            metrics.AverageDaysBetweenPurchases));
    }

    [HttpGet("{customerId}/timeline")]
    public async Task<ActionResult<CustomerTimelineResponseDto>> GetTimeline(
        [FromRoute] string customerId,
        [FromQuery] string granularity = "monthly",
        [FromQuery] string metric = "revenue",
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 365);
        var normalizedGranularity = granularity.Trim().ToLowerInvariant();
        var normalizedMetric = metric.Trim().ToLowerInvariant();

        List<CustomerEvolutionPointDto> aggregated;
        if (normalizedGranularity == "daily")
        {
            aggregated = await dbContext.CustomerSummariesDaily.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.ReferenceDate >= period.CurrentFrom && x.ReferenceDate < period.CurrentTo)
                .GroupBy(x => x.ReferenceDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
        }
        else if (normalizedGranularity == "weekly")
        {
            aggregated = await dbContext.CustomerSummariesWeekly.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate >= period.CurrentFrom && x.WeekStartDate < period.CurrentTo)
                .GroupBy(x => x.WeekStartDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            normalizedGranularity = "monthly";
            aggregated = await dbContext.CustomerSummariesMonthly.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.MonthStartDate >= period.CurrentFrom && x.MonthStartDate < period.CurrentTo)
                .GroupBy(x => x.MonthStartDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
        }

        if (aggregated.Count == 0)
        {
            aggregated = await BuildTimelineFromTransactionsAsync(
                resolvedCustomerCode,
                normalizedGranularity,
                period.CurrentFrom,
                period.CurrentTo,
                cancellationToken);
        }

        var points = aggregated.Select(x => new CustomerTimelinePointDto(
            x.PeriodStart,
            ResolveMetricValue(x, normalizedMetric),
            x.Revenue,
            x.Quantity,
            x.Weight,
            x.Orders)).ToList();

        return Ok(new CustomerTimelineResponseDto(normalizedGranularity, normalizedMetric, points));
    }

    [HttpGet("{customerId}/top-products")]
    public async Task<ActionResult<IReadOnlyList<CustomerProductItemDto>>> GetTopProducts(
        [FromRoute] string customerId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 365);
        var groupedRows = await dbContext.CommercialTransactions.AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.TransactionDate >= period.CurrentFrom && x.TransactionDate < period.CurrentTo)
            .GroupBy(x => new { x.ProductCode, x.ProductDescription })
            .Select(g => new
            {
                g.Key.ProductCode,
                g.Key.ProductDescription,
                Quantity = g.Sum(x => (double)x.Quantity),
                Revenue = g.Sum(x => (double)x.TotalAmount)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(TopProductsLimit)
            .ToListAsync(cancellationToken);

        var grouped = groupedRows
            .Select(x => new CustomerProductShareMetrics(x.ProductCode, x.ProductDescription, (decimal)x.Quantity, (decimal)x.Revenue, 0m))
            .ToList();

        var items = CustomerCalculators.BuildProductShares(grouped)
            .Select(x => new CustomerProductItemDto(x.ProductCode, x.ProductDescription, x.Quantity, x.Revenue, x.SharePercent))
            .ToList();

        return Ok(items);
    }

    [HttpGet("{customerId}/purchase-history")]
    public async Task<ActionResult<CustomerPurchaseHistoryResponseDto>> GetPurchaseHistory(
        [FromRoute] string customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 365);
        var query = dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.TransactionDate >= period.CurrentFrom && x.TransactionDate < period.CurrentTo);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.DocumentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerPurchaseHistoryItemDto(
                x.TransactionDate,
                x.DocumentNumber,
                $"{x.ProductCode} - {x.ProductDescription}",
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.GrossWeightKg,
                x.TransactionType))
            .ToListAsync(cancellationToken);

        return Ok(new CustomerPurchaseHistoryResponseDto(page, pageSize, totalItems, items));
    }

    [HttpGet("{customerId}/comparison")]
    public async Task<ActionResult<CustomerComparisonResponseDto>> GetComparison(
        [FromRoute] string customerId,
        [FromQuery] DateTime? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var windows = PeriodComparisonCalculator.BuildWindows(referenceDate ?? DateTime.UtcNow);
        var items = new List<CustomerComparisonItemDto>();
        foreach (var window in windows)
        {
            var current = await SumRevenueForComparisonWindow(resolvedCustomerCode, window.Granularity, window.CurrentFrom, window.CurrentToExclusive, cancellationToken);
            var previous = await SumRevenueForComparisonWindow(resolvedCustomerCode, window.Granularity, window.PreviousFrom, window.PreviousToExclusive, cancellationToken);
            var metrics = PeriodComparisonCalculator.BuildMetrics(window.Label, current, previous);
            items.Add(new CustomerComparisonItemDto(metrics.Label, metrics.CurrentValue, metrics.PreviousValue, metrics.VariationPercent));
        }

        return Ok(new CustomerComparisonResponseDto(items));
    }

    [HttpGet("{customerId}/insights")]
    public async Task<ActionResult<CustomerInsightsResponseDto>> GetInsights(
        [FromRoute] string customerId,
        [FromQuery] int movingAverageWindowMonths = 3,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return BadRequest("CustomerId inválido.");
        }

        if (movingAverageWindowMonths is not (3 or 6 or 12))
        {
            return BadRequest("movingAverageWindowMonths deve ser 3, 6 ou 12.");
        }

        var resolvedCustomerCode = await ResolveCustomerCodeAsync(normalizedId, cancellationToken);
        if (resolvedCustomerCode is null)
        {
            return NotFound("Cliente não encontrado para o identificador informado.");
        }

        var purchaseDays = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .Select(x => x.TransactionDate.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var summaryMonthlyRows = await dbContext.CustomerSummariesMonthly
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .GroupBy(x => x.MonthStartDate.Date)
            .Select(g => new
            {
                MonthStart = g.Key,
                Revenue = (decimal)g.Sum(x => (double)x.Revenue),
                Quantity = (decimal)g.Sum(x => (double)x.Quantity)
            })
            .OrderBy(x => x.MonthStart)
            .ToListAsync(cancellationToken);

        var monthlyRows = summaryMonthlyRows
            .Select(x => new CustomerMonthlyMetric(x.MonthStart, x.Revenue, x.Quantity))
            .ToList();

        if (monthlyRows.Count == 0)
        {
            var transactionMonthlyRows = await dbContext.CommercialTransactions
                .AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode)
                .GroupBy(x => new { x.TransactionDate.Year, x.TransactionDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.TotalAmount),
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToListAsync(cancellationToken);

            monthlyRows = transactionMonthlyRows
                .Select(x => new CustomerMonthlyMetric(
                    new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    x.Revenue,
                    x.Quantity))
                .OrderBy(x => x.MonthStart)
                .ToList();
        }

        var metrics = CustomerCalculators.BuildInsights(
            purchaseDays,
            monthlyRows,
            movingAverageWindowMonths,
            DateTime.UtcNow);

        return Ok(new CustomerInsightsResponseDto(
            metrics.AveragePurchaseFrequencyDays,
            metrics.EstimatedNextPurchaseDate,
            metrics.PredictedRevenue,
            metrics.PredictedQuantity,
            metrics.ConsumptionTrend,
            metrics.RiskLevel,
            metrics.DaysWithoutPurchase,
            metrics.RiskScore,
            metrics.FrequencyReason,
            metrics.NextPurchaseReason,
            metrics.RevenuePredictionReason,
            metrics.QuantityPredictionReason,
            metrics.RiskReason,
            metrics.MonthlyHistoryPeriods));
    }

    private async Task<List<CustomerMetricTransaction>> LoadCustomerMetricTransactions(string customerCode, CancellationToken cancellationToken)
    {
        return await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == customerCode)
            .OrderBy(x => x.TransactionDate)
            .Select(x => new CustomerMetricTransaction(
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.DocumentNumber,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg,
                x.TransactionDate))
            .ToListAsync(cancellationToken);
    }

    private async Task<decimal> SumRevenueMonthly(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerSummariesMonthly.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.MonthStartDate >= fromInclusive && x.MonthStartDate < toExclusive);

        if (!await query.AnyAsync(cancellationToken))
        {
            return await SumRevenueFromTransactions(customerCode, fromInclusive, toExclusive, cancellationToken);
        }

        var value = await query.SumAsync(x => (double)x.Revenue, cancellationToken);
        return (decimal)value;
    }

    private async Task<List<CustomerEvolutionPointDto>> BuildTimelineFromTransactionsAsync(
        string customerCode,
        string granularity,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.TransactionDate >= fromInclusive && x.TransactionDate < toExclusive)
            .Select(x => new
            {
                x.TransactionDate,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg,
                x.DocumentNumber
            })
            .ToListAsync(cancellationToken);

        DateTime ResolveBucket(DateTime transactionDate)
        {
            var utcDate = CustomerCalculators.NormalizeToUtc(transactionDate).Date;
            return granularity switch
            {
                "daily" => utcDate,
                "weekly" => CustomerCalculators.StartOfWeekUtc(utcDate),
                _ => new DateTime(utcDate.Year, utcDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        return rows
            .GroupBy(x => ResolveBucket(x.TransactionDate))
            .OrderBy(x => x.Key)
            .Select(g => new CustomerEvolutionPointDto(
                g.Key,
                g.Sum(x => x.TotalAmount),
                g.Sum(x => x.Quantity),
                g.Sum(x => x.GrossWeightKg),
                g.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .ToList();
    }

    private async Task<decimal> SumRevenueWeekly(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerSummariesWeekly.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.WeekStartDate >= fromInclusive && x.WeekStartDate < toExclusive);

        if (!await query.AnyAsync(cancellationToken))
        {
            return await SumRevenueFromTransactions(customerCode, fromInclusive, toExclusive, cancellationToken);
        }

        var value = await query.SumAsync(x => (double)x.Revenue, cancellationToken);
        return (decimal)value;
    }

    private async Task<decimal> SumRevenueForComparisonWindow(
        string customerCode,
        PeriodComparisonGranularity granularity,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        return granularity switch
        {
            PeriodComparisonGranularity.Monthly => await SumRevenueMonthly(customerCode, fromInclusive, toExclusive, cancellationToken),
            PeriodComparisonGranularity.Weekly => await SumRevenueWeekly(customerCode, fromInclusive, toExclusive, cancellationToken),
            _ => await SumRevenueDaily(customerCode, fromInclusive, toExclusive, cancellationToken)
        };
    }

    private async Task<decimal> SumRevenueDaily(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerSummariesDaily.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.ReferenceDate >= fromInclusive && x.ReferenceDate < toExclusive);

        if (!await query.AnyAsync(cancellationToken))
        {
            return await SumRevenueFromTransactions(customerCode, fromInclusive, toExclusive, cancellationToken);
        }

        var value = await query.SumAsync(x => (double)x.Revenue, cancellationToken);
        return (decimal)value;
    }

    private async Task<decimal> SumRevenueFromTransactions(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var value = await dbContext.CommercialTransactions.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.TransactionDate >= fromInclusive && x.TransactionDate < toExclusive)
            .SumAsync(x => (double)x.TotalAmount, cancellationToken);
        return (decimal)value;
    }

    private static decimal ResolveMetricValue(CustomerEvolutionPointDto point, string metric)
    {
        return metric switch
        {
            "quantity" => point.Quantity,
            "weight" => point.Weight,
            "orders" => point.Orders,
            _ => point.Revenue
        };
    }

    private async Task<string?> ResolveCustomerCodeAsync(string customerIdentifier, CancellationToken cancellationToken)
    {
        var normalized = customerIdentifier.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var resolvedFromTransactions = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x =>
                x.CustomerCode.ToUpper() == normalized ||
                x.CustomerName.ToUpper() == normalized)
            .Select(x => x.CustomerCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(resolvedFromTransactions))
        {
            return resolvedFromTransactions.Trim();
        }

        var resolvedFromCustomers = await dbContext.Customers
            .AsNoTracking()
            .Where(x =>
                x.CustomerCode.ToUpper() == normalized ||
                x.Name.ToUpper() == normalized)
            .Select(x => x.CustomerCode)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(resolvedFromCustomers) ? null : resolvedFromCustomers.Trim();
    }
}
