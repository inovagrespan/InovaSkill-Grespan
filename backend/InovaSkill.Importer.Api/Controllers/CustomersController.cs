using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet("{customerId}/summary")]
    public async Task<ActionResult<CustomerSummaryResponseDto>> GetSummary(
        [FromRoute] string customerId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        ResolvePeriods(dateFrom, dateTo, out var from, out var to, out _, out _);
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

        var allCustomerRows = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode)
            .OrderBy(x => x.TransactionDate)
            .Select(x => new
            {
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.DocumentNumber,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg,
                x.TransactionDate
            })
            .ToListAsync(cancellationToken);

        var rows = allCustomerRows
            .Where(x => x.TransactionDate >= from && x.TransactionDate < to)
            .ToList();

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

        var purchaseDays = rows.Select(x => x.TransactionDate.Date).Distinct().OrderBy(x => x).ToList();
        decimal? averageDaysBetweenPurchases = null;
        if (purchaseDays.Count > 1)
        {
            var deltas = purchaseDays.Zip(purchaseDays.Skip(1), (a, b) => (decimal)(b - a).TotalDays).ToList();
            averageDaysBetweenPurchases = deltas.Count == 0 ? null : deltas.Average();
        }

        var totalRevenue = rows.Sum(x => x.TotalAmount);
        var totalOrders = rows.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var lastPurchaseDate = allCustomerRows.Max(x => x.TransactionDate);
        var latestWeekStart = StartOfWeekUtc(DateTime.UtcNow);
        var currentWeekRevenue = (decimal)await dbContext.CustomerSummariesWeekly
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate == latestWeekStart)
            .SumAsync(x => (double)x.Revenue, cancellationToken);
        var previousWeekRevenue = (decimal)await dbContext.CustomerSummariesWeekly
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate == latestWeekStart.AddDays(-7))
            .SumAsync(x => (double)x.Revenue, cancellationToken);

        var status = ResolveStatus(lastPurchaseDate, currentWeekRevenue, previousWeekRevenue);

        var monthlyBuckets = rows
            .GroupBy(x => new DateTime(x.TransactionDate.Year, x.TransactionDate.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .Select(g => g.Sum(x => x.TotalAmount))
            .ToList();

        var weeklyBuckets = rows
            .GroupBy(x => StartOfWeekUtc(x.TransactionDate))
            .Select(g => g.Sum(x => x.TotalAmount))
            .ToList();

        // Regra: média por períodos com compra no intervalo filtrado.
        decimal? averageRevenueMonthly = monthlyBuckets.Count == 0 ? null : monthlyBuckets.Average();
        decimal? averageRevenueWeekly = weeklyBuckets.Count == 0 ? null : weeklyBuckets.Average();
        decimal? averageTicket = totalOrders == 0 ? null : totalRevenue / totalOrders;

        return Ok(new CustomerSummaryResponseDto(
            resolvedCustomerCode,
            baseRow.CustomerName,
            baseRow.City,
            customer?.Name ?? baseRow.CustomerName,
            lastPurchaseDate,
            status,
            totalRevenue,
            averageTicket,
            averageRevenueMonthly,
            averageRevenueWeekly,
            rows.Sum(x => x.Quantity),
            rows.Sum(x => x.GrossWeightKg),
            totalOrders,
            averageDaysBetweenPurchases));
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
        ResolvePeriods(dateFrom, dateTo, out var from, out var to, out _, out _);
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

        var normalizedGranularity = granularity.Trim().ToLowerInvariant();
        var normalizedMetric = metric.Trim().ToLowerInvariant();

        List<CustomerEvolutionPointDto> aggregated;
        if (normalizedGranularity == "daily")
        {
            aggregated = await dbContext.CustomerSummariesDaily.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.ReferenceDate >= from && x.ReferenceDate < to)
                .GroupBy(x => x.ReferenceDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
        }
        else if (normalizedGranularity == "weekly")
        {
            aggregated = await dbContext.CustomerSummariesWeekly.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.WeekStartDate >= from && x.WeekStartDate < to)
                .GroupBy(x => x.WeekStartDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            normalizedGranularity = "monthly";
            aggregated = await dbContext.CustomerSummariesMonthly.AsNoTracking()
                .Where(x => x.CustomerCode == resolvedCustomerCode && x.MonthStartDate >= from && x.MonthStartDate < to)
                .GroupBy(x => x.MonthStartDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, (decimal)g.Sum(x => (double)x.Revenue), (decimal)g.Sum(x => (double)x.Quantity), (decimal)g.Sum(x => (double)x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
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
        ResolvePeriods(dateFrom, dateTo, out var from, out var to, out _, out _);
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

        var grouped = await dbContext.CommercialTransactions.AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.TransactionDate >= from && x.TransactionDate < to)
            .GroupBy(x => new { x.ProductCode, x.ProductDescription })
            .Select(g => new
            {
                g.Key.ProductCode,
                g.Key.ProductDescription,
                Quantity = (decimal)g.Sum(x => (double)x.Quantity),
                Revenue = (decimal)g.Sum(x => (double)x.TotalAmount)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(50)
            .ToListAsync(cancellationToken);

        var totalRevenue = grouped.Sum(x => x.Revenue);
        var items = grouped.Select(x => new CustomerProductItemDto(
            x.ProductCode,
            x.ProductDescription,
            x.Quantity,
            x.Revenue,
            totalRevenue == 0 ? 0 : (x.Revenue / totalRevenue) * 100m)).ToList();

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
        ResolvePeriods(dateFrom, dateTo, out var from, out var to, out _, out _);
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

        var query = dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == resolvedCustomerCode && x.TransactionDate >= from && x.TransactionDate < to);

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

        var reference = NormalizeToUtc(referenceDate ?? DateTime.UtcNow).Date;
        var currentMonthStart = new DateTime(reference.Year, reference.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var currentMonth = await SumRevenueMonthly(resolvedCustomerCode, currentMonthStart, nextMonthStart, cancellationToken);
        var previousMonth = await SumRevenueMonthly(resolvedCustomerCode, previousMonthStart, currentMonthStart, cancellationToken);

        var currentWeekStart = StartOfWeekUtc(reference);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var currentWeek = await SumRevenueWeekly(resolvedCustomerCode, currentWeekStart, currentWeekStart.AddDays(7), cancellationToken);
        var previousWeek = await SumRevenueWeekly(resolvedCustomerCode, previousWeekStart, currentWeekStart, cancellationToken);

        var current30Start = reference.AddDays(-29);
        var previous30Start = current30Start.AddDays(-30);
        var previous30End = current30Start.AddDays(-1);
        var current30 = await SumRevenueDaily(resolvedCustomerCode, current30Start, reference, cancellationToken);
        var previous30 = await SumRevenueDaily(resolvedCustomerCode, previous30Start, previous30End, cancellationToken);

        var items = new List<CustomerComparisonItemDto>
        {
            BuildComparison("Este mês vs mês anterior", currentMonth, previousMonth),
            BuildComparison("Esta semana vs semana anterior", currentWeek, previousWeek),
            BuildComparison("Últimos 30 dias vs 30 dias anteriores", current30, previous30)
        };

        return Ok(new CustomerComparisonResponseDto(items));
    }

    private static CustomerComparisonItemDto BuildComparison(string label, decimal current, decimal previous)
    {
        decimal? variation = previous == 0 ? null : ((current - previous) / previous) * 100m;
        return new CustomerComparisonItemDto(label, current, previous, variation);
    }

    private async Task<decimal> SumRevenueMonthly(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var value = await dbContext.CustomerSummariesMonthly.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.MonthStartDate >= fromInclusive && x.MonthStartDate < toExclusive)
            .SumAsync(x => (double)x.Revenue, cancellationToken);
        return (decimal)value;
    }

    private async Task<decimal> SumRevenueWeekly(string customerCode, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var value = await dbContext.CustomerSummariesWeekly.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.WeekStartDate >= fromInclusive && x.WeekStartDate < toExclusive)
            .SumAsync(x => (double)x.Revenue, cancellationToken);
        return (decimal)value;
    }

    private async Task<decimal> SumRevenueDaily(string customerCode, DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken)
    {
        var toExclusive = toInclusive.AddDays(1);
        var value = await dbContext.CustomerSummariesDaily.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.ReferenceDate >= fromInclusive && x.ReferenceDate < toExclusive)
            .SumAsync(x => (double)x.Revenue, cancellationToken);
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

    private static string ResolveStatus(DateTime? lastPurchaseDate, decimal currentWeekRevenue, decimal previousWeekRevenue)
    {
        if (lastPurchaseDate is null)
        {
            return "Inativo";
        }

        var daysWithoutPurchase = (DateTime.UtcNow.Date - lastPurchaseDate.Value.Date).TotalDays;
        if (daysWithoutPurchase > 45)
        {
            return "Inativo";
        }

        if (previousWeekRevenue <= 0)
        {
            return "Ativo";
        }

        var variation = ((currentWeekRevenue - previousWeekRevenue) / previousWeekRevenue) * 100m;
        if (variation >= 10m) return "Em crescimento";
        if (variation <= -10m) return "Em queda";
        return "Ativo";
    }

    private static DateTime StartOfWeekUtc(DateTime date)
    {
        var normalized = NormalizeToUtc(date).Date;
        var diff = ((int)normalized.DayOfWeek + 6) % 7;
        return normalized.AddDays(-diff);
    }

    private static void ResolvePeriods(DateTime? dateFrom, DateTime? dateTo, out DateTime currentFrom, out DateTime currentTo, out DateTime previousFrom, out DateTime previousTo)
    {
        var normalizedTo = NormalizeToUtc(dateTo ?? DateTime.UtcNow);
        var to = normalizedTo.Date.AddDays(1);
        var from = NormalizeToUtc(dateFrom ?? to.AddDays(-365)).Date;
        if (from >= to)
        {
            from = to.AddDays(-365);
        }

        var length = to - from;
        currentFrom = from;
        currentTo = to;
        previousTo = from;
        previousFrom = from - length;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
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
