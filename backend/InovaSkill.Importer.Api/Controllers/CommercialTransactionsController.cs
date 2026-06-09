using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/commercial-transactions")]
public sealed class CommercialTransactionsController(ImportDbContext dbContext) : ControllerBase
{
    private const int MinPageSize = 10;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CommercialTransactionDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? documentNumber = null,
        [FromQuery] string? customerCode = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var query = ApplyFilters(
            dbContext.CommercialTransactions.AsNoTracking(),
            documentNumber,
            customerCode,
            customerName,
            productCode,
            city,
            productGroup,
            transactionType,
            dateFrom,
            dateTo);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CommercialTransactionDto(
                x.Id,
                x.DocumentNumber,
                x.TransactionDate,
                x.CustomerCode,
                x.CustomerName,
                x.ProductCode,
                x.ProductDescription,
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.TransactionType,
                x.City,
                x.ProductGroup,
                x.GrossWeightKg,
                x.SourceFileJobId))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<CommercialTransactionDto>(page, pageSize, total, items));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<CommercialTransactionSummaryResponseDto>> GetSummary(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string granularity = "weekly",
        [FromQuery] string sortBy = "growth",
        [FromQuery] DateTime? referenceDate = null,
        [FromQuery] string? documentNumber = null,
        [FromQuery] string? customerCode = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var query = ApplyFilters(
            dbContext.CommercialTransactions.AsNoTracking(),
            documentNumber,
            customerCode,
            customerName,
            productCode,
            city,
            productGroup,
            transactionType,
            dateFrom,
            dateTo);

        var rows = await query
            .Select(x => new Domain.Entities.CommercialTransaction
            {
                TransactionDate = x.TransactionDate,
                CustomerName = x.CustomerName,
                TotalAmount = x.TotalAmount,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                GrossWeightKg = x.GrossWeightKg
            })
            .ToListAsync(cancellationToken);

        var resolvedGranularity = ResolveSummaryGranularity(granularity);
        var resolvedSortBy = ResolveSummarySortBy(sortBy);

        var summary = SalesCompanySummaryCalculator.Build(rows, resolvedGranularity, resolvedSortBy, referenceDate);
        var pagedItems = summary.Items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new CommercialTransactionSummaryResponseDto(
            page,
            pageSize,
            summary.TotalCompanies,
            ToSummaryGranularityName(resolvedGranularity),
            summary.CurrentPeriodStart,
            summary.PreviousPeriodStart,
            summary.CurrentPeriodTotalAmount,
            summary.PreviousPeriodTotalAmount,
            summary.TotalGrowthPercent,
            summary.TotalRecords,
            summary.TotalAmount,
            summary.TotalQuantity,
            summary.TotalWeightKg,
            summary.TotalCompanies,
            pagedItems.Select(x => new CommercialTransactionCompanySummaryDto(
                x.CompanyName,
                x.TotalAmount,
                x.TotalQuantity,
                x.TotalWeightKg,
                x.CurrentPeriodAmount,
                x.PreviousPeriodAmount,
                x.GrowthPercent)).ToList());

        return Ok(response);
    }

    [HttpGet("timeline")]
    public async Task<ActionResult<CommercialTransactionTimelineResponseDto>> GetTimeline(
        [FromQuery] string granularity = "monthly",
        [FromQuery] string? documentNumber = null,
        [FromQuery] string? customerCode = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? city = null,
        [FromQuery] string? productGroup = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedGranularity = ResolveTimelineGranularity(granularity);

        var rows = await ApplyFilters(
                dbContext.CommercialTransactions.AsNoTracking(),
                documentNumber,
                customerCode,
                customerName,
                productCode,
                city,
                productGroup,
                transactionType,
                dateFrom,
                dateTo)
            .Select(x => new
            {
                x.TransactionDate,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .GroupBy(x => GetTimelinePeriodStart(x.TransactionDate, resolvedGranularity))
            .OrderBy(x => x.Key)
            .Select(group => new CommercialTransactionTimelinePointDto(
                group.Key,
                group.Sum(x => x.TotalAmount),
                group.Sum(x => x.Quantity),
                group.Sum(x => x.GrossWeightKg),
                group.Count()))
            .ToList();

        return Ok(new CommercialTransactionTimelineResponseDto(ToTimelineGranularityName(resolvedGranularity), items));
    }

    private static IQueryable<Domain.Entities.CommercialTransaction> ApplyFilters(
        IQueryable<Domain.Entities.CommercialTransaction> query,
        string? documentNumber,
        string? customerCode,
        string? customerName,
        string? productCode,
        string? city,
        string? productGroup,
        string? transactionType,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        if (!string.IsNullOrWhiteSpace(documentNumber))
        {
            var normalized = documentNumber.Trim();
            query = query.Where(x => x.DocumentNumber.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            var normalized = customerCode.Trim();
            query = query.Where(x => x.CustomerCode.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var normalized = customerName.Trim();
            query = query.Where(x => x.CustomerName.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var normalized = productCode.Trim();
            query = query.Where(x => x.ProductCode.Contains(normalized));
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

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var normalized = transactionType.Trim();
            query = query.Where(x => x.TransactionType.Contains(normalized));
        }

        if (dateFrom.HasValue)
        {
            var from = NormalizeToUtc(dateFrom.Value).Date;
            query = query.Where(x => x.TransactionDate >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = NormalizeToUtc(dateTo.Value).Date.AddDays(1);
            query = query.Where(x => x.TransactionDate < toExclusive);
        }

        return query;
    }

    private static SalesSummaryGranularity ResolveSummaryGranularity(string granularity)
    {
        return granularity.Trim().ToLowerInvariant() switch
        {
            "daily" => SalesSummaryGranularity.Daily,
            "monthly" => SalesSummaryGranularity.Monthly,
            _ => SalesSummaryGranularity.Weekly
        };
    }

    private static SalesSummarySortBy ResolveSummarySortBy(string sortBy)
    {
        return sortBy.Trim().ToLowerInvariant() switch
        {
            "amount" => SalesSummarySortBy.Amount,
            "weight" => SalesSummarySortBy.Weight,
            "quantity" => SalesSummarySortBy.Quantity,
            _ => SalesSummarySortBy.Growth
        };
    }

    private static TimelineGranularity ResolveTimelineGranularity(string granularity)
    {
        return granularity.Trim().ToLowerInvariant() switch
        {
            "daily" => TimelineGranularity.Daily,
            "weekly" => TimelineGranularity.Weekly,
            _ => TimelineGranularity.Monthly
        };
    }

    private static string ToSummaryGranularityName(SalesSummaryGranularity granularity)
    {
        return granularity switch
        {
            SalesSummaryGranularity.Daily => "daily",
            SalesSummaryGranularity.Monthly => "monthly",
            _ => "weekly"
        };
    }

    private static string ToTimelineGranularityName(TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Daily => "daily",
            TimelineGranularity.Weekly => "weekly",
            _ => "monthly"
        };
    }

    private static DateTime GetTimelinePeriodStart(DateTime transactionDate, TimelineGranularity granularity)
    {
        var normalized = NormalizeToUtc(transactionDate).Date;

        return granularity switch
        {
            TimelineGranularity.Daily => normalized,
            TimelineGranularity.Weekly => normalized.AddDays(-((int)normalized.DayOfWeek + 6) % 7),
            _ => new DateTime(normalized.Year, normalized.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };
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

    private enum TimelineGranularity
    {
        Daily,
        Weekly,
        Monthly
    }
}
