using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Analytics;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/customer-analytics-v2")]
public sealed class CustomerAnalyticsV2Controller(ImportDbContext dbContext) : ControllerBase
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
        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 30);

        var currentRows = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= period.CurrentFrom && x.TransactionDate < period.CurrentTo)
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

        var previousCustomerKeys = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= period.PreviousFrom && x.TransactionDate < period.PreviousTo)
            .Select(x => $"{x.CustomerCode}|{x.CustomerName}")
            .Distinct()
            .ToListAsync(cancellationToken);

        var metrics = CustomerCalculators.BuildAnalyticsSummary(currentRows, previousCustomerKeys);

        return Ok(new CustomerAnalyticsSummaryDto(
            metrics.ActiveCustomers,
            metrics.TotalRevenue,
            metrics.TotalOrders,
            metrics.AverageTicket,
            metrics.AverageRevenuePerCustomer,
            metrics.NewCustomers,
            metrics.InactiveCustomers,
            period.CurrentFrom,
            period.CurrentTo.AddTicks(-1),
            period.PreviousFrom,
            period.PreviousTo.AddTicks(-1)));
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

        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 30);
        var currentRows = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= period.CurrentFrom && x.TransactionDate < period.CurrentTo)
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

        var previousRevenue = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .Where(x => x.TransactionDate >= period.PreviousFrom && x.TransactionDate < period.PreviousTo)
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(g => new
            {
                Key = $"{g.Key.CustomerCode}|{g.Key.CustomerName}",
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToDictionaryAsync(x => x.Key, x => x.Revenue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var items = CustomerCalculators.BuildRanking(currentRows, previousRevenue, sortBy)
            .Select(x => new CustomerRankingItemDto(
                x.CustomerCode,
                x.CustomerName,
                x.Revenue,
                x.Quantity,
                x.Weight,
                x.Orders,
                x.AverageTicket,
                x.VariationPercent))
            .ToList();

        var total = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new CustomerRankingResponseDto(page, pageSize, total, paged));
    }

    [HttpGet("new-customers-monthly")]
    public async Task<ActionResult<CustomerNewCustomersMonthlyResponseDto>> GetNewCustomersMonthly(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? transactionType = null,
        CancellationToken cancellationToken = default)
    {
        var period = CustomerCalculators.ResolvePeriods(dateFrom, dateTo, defaultDays: 30);

        var firstPurchaseDates = await BuildTransactionsQuery(customer, city, productGroup, productCode, transactionType)
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(g => g.Min(x => x.TransactionDate))
            .Where(x => x >= period.CurrentFrom && x < period.CurrentTo)
            .ToListAsync(cancellationToken);

        var points = CustomerCalculators.BuildNewCustomersMonthly(firstPurchaseDates, period.CurrentFrom, period.CurrentTo)
            .Select(x => new CustomerNewCustomersMonthlyPointDto(x.MonthStart, x.NewCustomers))
            .ToList();

        return Ok(new CustomerNewCustomersMonthlyResponseDto(
            period.CurrentFrom,
            period.CurrentTo.AddTicks(-1),
            points.Sum(x => x.NewCustomers),
            points.Count(x => x.NewCustomers > 0),
            points));
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
}
