using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Analytics;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/finance")]
public sealed class FinanceController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("dashboard")]
    public async Task<ActionResult<FinanceDashboardResponseDto>> GetDashboard(
        [FromQuery] string customer = "",
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] bool allTime = true,
        [FromQuery] string revenueGranularity = "monthly",
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(DefaultPage, page);
        pageSize = Math.Clamp(pageSize, 10, MaxPageSize);

        var customers = await LoadCustomersAsync(cancellationToken);
        var items = await LoadDashboardItemsAsync(customer, dateFrom, dateTo, allTime, cancellationToken);
        var summary = FinanceDashboardCalculator.BuildSummary(items);
        var trend = FinanceDashboardCalculator.BuildRevenueTrend(items, revenueGranularity);
        var ranking = FinanceDashboardCalculator.BuildCustomerRevenueRanking(items);
        var totalItems = items.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        var resolvedPage = Math.Min(page, totalPages);
        var pagedItems = items
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Customer, StringComparer.OrdinalIgnoreCase)
            .Skip((resolvedPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new FinanceDashboardResponseDto(
            customers,
            new FinanceDashboardSummaryDto(summary.TotalRevenue, summary.TotalOrders, summary.TotalQuantity, summary.AverageTicket),
            trend.Select(x => new FinanceRevenueTrendPointDto(x.Period, x.Label, x.Revenue)).ToList(),
            ranking.Select(x => new FinanceCustomerRevenuePointDto(x.Customer, x.Revenue)).ToList(),
            pagedItems.Select(x => new FinanceDashboardItemDto(x.Customer, x.Date, x.Revenue, x.Orders, x.Quantity)).ToList(),
            resolvedPage,
            pageSize,
            totalItems,
            totalPages));
    }

    private async Task<IReadOnlyList<string>> LoadCustomersAsync(CancellationToken cancellationToken)
    {
        var summaryCustomers = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Select(x => x.CustomerName.Trim())
            .Where(x => x != string.Empty)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        if (summaryCustomers.Count > 0)
        {
            return summaryCustomers;
        }

        return await dbContext.CommercialTransactions
            .AsNoTracking()
            .Select(x => x.CustomerName.Trim())
            .Where(x => x != string.Empty)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<FinanceDashboardItem>> LoadDashboardItemsAsync(
        string customer,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool allTime,
        CancellationToken cancellationToken)
    {
        var summaryItems = await LoadItemsFromDailySummariesAsync(customer, dateFrom, dateTo, allTime, cancellationToken);
        if (summaryItems.Count > 0)
        {
            return summaryItems;
        }

        var transactions = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Select(x => new FinanceDashboardTransaction(
                x.CustomerName,
                x.TransactionDate,
                x.TotalAmount,
                x.DocumentNumber,
                x.Quantity))
            .ToListAsync(cancellationToken);

        return FinanceDashboardCalculator.BuildItems(transactions, customer, dateFrom, dateTo, allTime).ToList();
    }

    private async Task<List<FinanceDashboardItem>> LoadItemsFromDailySummariesAsync(
        string customer,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool allTime,
        CancellationToken cancellationToken)
    {
        var query = ApplySummaryFilters(
            dbContext.CustomerSummariesDaily.AsNoTracking(),
            customer,
            dateFrom,
            dateTo,
            allTime);

        return await query
            .GroupBy(x => new { Customer = x.CustomerName, Date = x.ReferenceDate.Date })
            .Select(g => new
            {
                g.Key.Customer,
                g.Key.Date,
                Revenue = g.Sum(x => (double)x.Revenue),
                Orders = g.Sum(x => x.Orders),
                Quantity = g.Sum(x => (double)x.Quantity)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Customer)
            .Select(x => new FinanceDashboardItem(
                x.Customer,
                x.Date,
                (decimal)x.Revenue,
                x.Orders,
                (decimal)x.Quantity))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<CustomerSummaryDaily> ApplySummaryFilters(
        IQueryable<CustomerSummaryDaily> query,
        string customer,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool allTime)
    {
        var normalizedCustomer = customer.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedCustomer))
        {
            query = query.Where(x => x.CustomerName.Contains(normalizedCustomer));
        }

        if (allTime)
        {
            return query;
        }

        var fromInclusive = dateFrom.HasValue ? NormalizeToUtc(dateFrom.Value).Date : (DateTime?)null;
        var toExclusive = dateTo.HasValue ? NormalizeToUtc(dateTo.Value).Date.AddDays(1) : (DateTime?)null;

        if (fromInclusive.HasValue)
        {
            query = query.Where(x => x.ReferenceDate >= fromInclusive.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(x => x.ReferenceDate < toExclusive.Value);
        }

        return query;
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
