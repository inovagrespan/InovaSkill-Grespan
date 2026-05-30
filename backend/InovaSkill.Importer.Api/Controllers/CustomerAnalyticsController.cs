using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/customer-analytics")]
public sealed class CustomerAnalyticsController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<CustomerAnalyticsSummaryDto>> GetSummary(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? transactionType = null,
        CancellationToken cancellationToken = default)
    {
        ResolvePeriods(dateFrom, dateTo, out var currentFrom, out var currentTo, out var previousFrom, out var previousTo);

        var currentQuery = BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= currentFrom && x.TransactionDate < currentTo);

        var previousQuery = BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= previousFrom && x.TransactionDate < previousTo);

        var currentRows = await currentQuery
            .Select(x => new { x.CustomerCode, x.CustomerName, x.DocumentNumber, x.TotalAmount })
            .ToListAsync(cancellationToken);

        var previousCustomers = await previousQuery
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeCustomers = currentRows
            .Select(x => $"{x.CustomerCode}|{x.CustomerName}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previousCustomerSet = previousCustomers
            .Select(x => $"{x.CustomerCode}|{x.CustomerName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var totalRevenue = currentRows.Sum(x => x.TotalAmount);
        var totalOrders = currentRows.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var newCustomers = activeCustomers.Count(x => !previousCustomerSet.Contains(x));
        var inactiveCustomers = previousCustomerSet.Count(x => !activeCustomers.Contains(x));
        var activeCount = activeCustomers.Count;

        var response = new CustomerAnalyticsSummaryDto(
            activeCount,
            totalRevenue,
            totalOrders,
            totalOrders == 0 ? 0 : totalRevenue / totalOrders,
            activeCount == 0 ? 0 : totalRevenue / activeCount,
            newCustomers,
            inactiveCustomers,
            currentFrom,
            currentTo.AddTicks(-1),
            previousFrom,
            previousTo.AddTicks(-1));

        return Ok(response);
    }

    [HttpGet("ranking")]
    public async Task<ActionResult<CustomerRankingResponseDto>> GetRanking(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] string sortBy = "revenue",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        ResolvePeriods(dateFrom, dateTo, out var currentFrom, out var currentTo, out var previousFrom, out var previousTo);

        var current = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= currentFrom && x.TransactionDate < currentTo)
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(g => new
            {
                g.Key.CustomerCode,
                g.Key.CustomerName,
                Revenue = g.Sum(x => x.TotalAmount),
                Quantity = g.Sum(x => x.Quantity),
                Weight = g.Sum(x => x.GrossWeightKg),
                Orders = g.Select(x => x.DocumentNumber).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var previousRevenue = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= previousFrom && x.TransactionDate < previousTo)
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(g => new
            {
                g.Key.CustomerCode,
                g.Key.CustomerName,
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToDictionaryAsync(
                x => $"{x.CustomerCode}|{x.CustomerName}",
                x => x.Revenue,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var items = current.Select(x =>
        {
            var key = $"{x.CustomerCode}|{x.CustomerName}";
            previousRevenue.TryGetValue(key, out var prev);
            decimal? variation = prev == 0 ? null : ((x.Revenue - prev) / prev) * 100m;
            return new CustomerRankingItemDto(
                x.CustomerCode,
                x.CustomerName,
                x.Revenue,
                x.Quantity,
                x.Weight,
                x.Orders,
                x.Orders == 0 ? 0 : x.Revenue / x.Orders,
                variation);
        }).ToList();

        items = sortBy.Trim().ToLowerInvariant() switch
        {
            "growth" => items.OrderByDescending(x => x.VariationPercent ?? decimal.MinValue).ToList(),
            "drop" => items.OrderBy(x => x.VariationPercent ?? decimal.MaxValue).ToList(),
            "quantity" => items.OrderByDescending(x => x.Quantity).ToList(),
            "weight" => items.OrderByDescending(x => x.Weight).ToList(),
            "ticket" => items.OrderByDescending(x => x.AverageTicket).ToList(),
            _ => items.OrderByDescending(x => x.Revenue).ToList()
        };

        var total = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new CustomerRankingResponseDto(page, pageSize, total, paged));
    }

    [HttpGet("{customerCode}/details")]
    public async Task<ActionResult<CustomerDetailsDto>> GetDetails(
        [FromRoute] string customerCode,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        ResolvePeriods(dateFrom, dateTo, out var currentFrom, out var currentTo, out _, out _);

        var rows = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.TransactionDate >= currentFrom && x.TransactionDate < currentTo)
            .OrderBy(x => x.TransactionDate)
            .Select(x => new { x.CustomerCode, x.CustomerName, x.TransactionDate, x.DocumentNumber, x.TotalAmount, x.Quantity, x.GrossWeightKg })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return NotFound();
        }

        var dates = rows.Select(x => x.TransactionDate.Date).Distinct().OrderBy(x => x).ToList();
        decimal? avgDays = null;
        if (dates.Count > 1)
        {
            var diffs = dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).ToList();
            avgDays = diffs.Count == 0 ? null : Convert.ToDecimal(diffs.Average());
        }

        var revenue = rows.Sum(x => x.TotalAmount);
        var orders = rows.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var dto = new CustomerDetailsDto(
            rows[0].CustomerCode,
            rows[0].CustomerName,
            revenue,
            rows.Sum(x => x.Quantity),
            rows.Sum(x => x.GrossWeightKg),
            orders,
            orders == 0 ? 0 : revenue / orders,
            rows.Max(x => x.TransactionDate),
            avgDays);

        return Ok(dto);
    }

    [HttpGet("{customerCode}/evolution")]
    public async Task<ActionResult<IReadOnlyList<CustomerEvolutionPointDto>>> GetEvolution(
        [FromRoute] string customerCode,
        [FromQuery] string granularity = "monthly",
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        ResolvePeriods(dateFrom, dateTo, out var currentFrom, out var currentTo, out _, out _);
        var resolved = granularity.Trim().ToLowerInvariant();

        if (resolved == "daily")
        {
            var rows = await dbContext.CustomerSummariesDaily.AsNoTracking()
                .Where(x => x.CustomerCode == customerCode && x.ReferenceDate >= currentFrom && x.ReferenceDate < currentTo)
                .GroupBy(x => x.ReferenceDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.Quantity), g.Sum(x => x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
            return Ok(rows);
        }

        if (resolved == "weekly")
        {
            var rows = await dbContext.CustomerSummariesWeekly.AsNoTracking()
                .Where(x => x.CustomerCode == customerCode && x.WeekStartDate >= currentFrom && x.WeekStartDate < currentTo)
                .GroupBy(x => x.WeekStartDate.Date)
                .OrderBy(x => x.Key)
                .Select(g => new CustomerEvolutionPointDto(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.Quantity), g.Sum(x => x.Weight), g.Sum(x => x.Orders)))
                .ToListAsync(cancellationToken);
            return Ok(rows);
        }

        var monthlyRows = await dbContext.CustomerSummariesMonthly.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.MonthStartDate >= currentFrom && x.MonthStartDate < currentTo)
            .GroupBy(x => x.MonthStartDate.Date)
            .OrderBy(x => x.Key)
            .Select(g => new CustomerEvolutionPointDto(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.Quantity), g.Sum(x => x.Weight), g.Sum(x => x.Orders)))
            .ToListAsync(cancellationToken);
        return Ok(monthlyRows);
    }

    [HttpGet("{customerCode}/top-products")]
    public async Task<ActionResult<IReadOnlyList<CustomerProductItemDto>>> GetTopProducts(
        [FromRoute] string customerCode,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        ResolvePeriods(dateFrom, dateTo, out var currentFrom, out var currentTo, out _, out _);

        var grouped = await dbContext.CommercialTransactions.AsNoTracking()
            .Where(x => x.CustomerCode == customerCode && x.TransactionDate >= currentFrom && x.TransactionDate < currentTo)
            .GroupBy(x => new { x.ProductCode, x.ProductDescription })
            .Select(g => new
            {
                g.Key.ProductCode,
                g.Key.ProductDescription,
                Quantity = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(20)
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

    private IQueryable<Domain.Entities.CommercialTransaction> BuildTransactionsQuery(
        string? customer,
        string? city,
        string? productGroup,
        string? productCode,
        string? transactionType)
    {
        var query = dbContext.CommercialTransactions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(customer))
        {
            var normalized = customer.Trim();
            query = query.Where(x => x.CustomerName.Contains(normalized) || x.CustomerCode.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalized = city.Trim();
            query = query.Where(x => x.City.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(productGroup))
        {
            var normalized = productGroup.Trim();
            query = query.Where(x => x.ProductGroup.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var normalized = productCode.Trim();
            query = query.Where(x => x.ProductCode.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var normalized = transactionType.Trim();
            query = query.Where(x => x.TransactionType.Contains(normalized));
        }

        return query;
    }

    private static void ResolvePeriods(DateTime? dateFrom, DateTime? dateTo, out DateTime currentFrom, out DateTime currentTo, out DateTime previousFrom, out DateTime previousTo)
    {
        var normalizedTo = NormalizeToUtc(dateTo ?? DateTime.UtcNow);
        var to = normalizedTo.Date.AddDays(1);
        var from = NormalizeToUtc(dateFrom ?? to.AddDays(-30)).Date;
        if (from >= to)
        {
            from = to.AddDays(-30);
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
}
