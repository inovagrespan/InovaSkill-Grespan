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
    private const int QuarterMonthSpan = 3;

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
                x.Quantity * x.UnitPrice,
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
                DocumentNumber = x.DocumentNumber,
                CustomerName = x.CustomerName,
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
                x.DocumentCount,
                x.SingleDocumentNumber,
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
        [FromQuery] string? groupBy = null,
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
        var resolvedGranularity = ResolveTimelineGranularity(groupBy ?? granularity);
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

        var items = resolvedGranularity == TimelineGranularity.Weekly
            ? await BuildWeeklyTimelineAsync(query, cancellationToken)
            : await BuildTimelineAsync(query, resolvedGranularity, cancellationToken);

        return Ok(new CommercialTransactionTimelineResponseDto(ToTimelineGranularityName(resolvedGranularity), items));
    }

    private static async Task<List<CommercialTransactionTimelinePointDto>> BuildTimelineAsync(
        IQueryable<Domain.Entities.CommercialTransaction> query,
        TimelineGranularity granularity,
        CancellationToken cancellationToken)
    {
        var grouped = granularity switch
        {
            TimelineGranularity.Hour => await query
                .GroupBy(x => new
                {
                    x.TransactionDate.Year,
                    x.TransactionDate.Month,
                    x.TransactionDate.Day,
                    x.TransactionDate.Hour
                })
                .Select(group => new TimelineAggregateRow(
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    group.Sum(x => (double)x.Quantity * (double)x.UnitPrice),
                    group.Sum(x => (double)x.Quantity),
                    group.Sum(x => (double)x.GrossWeightKg),
                    group.Count()))
                .ToListAsync(cancellationToken),

            TimelineGranularity.Day => await query
                .GroupBy(x => new
                {
                    x.TransactionDate.Year,
                    x.TransactionDate.Month,
                    x.TransactionDate.Day
                })
                .Select(group => new TimelineAggregateRow(
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    0,
                    group.Sum(x => (double)x.Quantity * (double)x.UnitPrice),
                    group.Sum(x => (double)x.Quantity),
                    group.Sum(x => (double)x.GrossWeightKg),
                    group.Count()))
                .ToListAsync(cancellationToken),

            TimelineGranularity.Quarter => await query
                .GroupBy(x => new
                {
                    x.TransactionDate.Year,
                    QuarterStartMonth = ((x.TransactionDate.Month - 1) / QuarterMonthSpan) * QuarterMonthSpan + 1
                })
                .Select(group => new TimelineAggregateRow(
                    group.Key.Year,
                    group.Key.QuarterStartMonth,
                    1,
                    0,
                    group.Sum(x => (double)x.Quantity * (double)x.UnitPrice),
                    group.Sum(x => (double)x.Quantity),
                    group.Sum(x => (double)x.GrossWeightKg),
                    group.Count()))
                .ToListAsync(cancellationToken),

            _ => await query
                .GroupBy(x => new
                {
                    x.TransactionDate.Year,
                    x.TransactionDate.Month
                })
                .Select(group => new TimelineAggregateRow(
                    group.Key.Year,
                    group.Key.Month,
                    1,
                    0,
                    group.Sum(x => (double)x.Quantity * (double)x.UnitPrice),
                    group.Sum(x => (double)x.Quantity),
                    group.Sum(x => (double)x.GrossWeightKg),
                    group.Count()))
                .ToListAsync(cancellationToken)
        };

        return grouped
            .Select(x => new CommercialTransactionTimelinePointDto(
                new DateTime(x.Year, x.Month, x.Day, x.Hour, 0, 0, DateTimeKind.Utc),
                (decimal)x.TotalAmount,
                (decimal)x.TotalQuantity,
                (decimal)x.TotalWeightKg,
                x.RecordCount))
            .OrderBy(x => x.PeriodStart)
            .ToList();
    }

    private static async Task<List<CommercialTransactionTimelinePointDto>> BuildWeeklyTimelineAsync(
        IQueryable<Domain.Entities.CommercialTransaction> query,
        CancellationToken cancellationToken)
    {
        var dailyRows = await query
            .GroupBy(x => new
            {
                x.TransactionDate.Year,
                x.TransactionDate.Month,
                x.TransactionDate.Day
            })
            .Select(group => new TimelineAggregateRow(
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                0,
                group.Sum(x => (double)x.Quantity * (double)x.UnitPrice),
                group.Sum(x => (double)x.Quantity),
                group.Sum(x => (double)x.GrossWeightKg),
                group.Count()))
            .ToListAsync(cancellationToken);

        return dailyRows
            .GroupBy(x => GetWeekStart(new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Utc)))
            .OrderBy(x => x.Key)
            .Select(group => new CommercialTransactionTimelinePointDto(
                group.Key,
                (decimal)group.Sum(x => x.TotalAmount),
                (decimal)group.Sum(x => x.TotalQuantity),
                (decimal)group.Sum(x => x.TotalWeightKg),
                group.Sum(x => x.RecordCount)))
            .ToList();
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
            var normalized = documentNumber.Trim().ToUpper();
            query = query.Where(x => x.DocumentNumber.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            var normalized = customerCode.Trim().ToUpper();
            query = query.Where(x => x.CustomerCode.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var normalized = customerName.Trim().ToUpper();
            query = query.Where(x => x.CustomerName.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var normalized = productCode.Trim().ToUpper();
            query = query.Where(x => x.ProductCode.ToUpper().Contains(normalized) || x.ProductDescription.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalized = city.Trim().ToUpper();
            query = query.Where(x => x.City.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(productGroup))
        {
            var normalized = productGroup.Trim().ToUpper();
            query = query.Where(x => x.ProductGroup.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var normalized = transactionType.Trim().ToUpper();
            query = query.Where(x => x.TransactionType.ToUpper().Contains(normalized));
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
            "day" => SalesSummaryGranularity.Daily,
            "monthly" => SalesSummaryGranularity.Monthly,
            "month" => SalesSummaryGranularity.Monthly,
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
            "hour" or "hourly" => TimelineGranularity.Hour,
            "daily" or "day" => TimelineGranularity.Day,
            "weekly" or "week" => TimelineGranularity.Weekly,
            "quarter" or "quarterly" => TimelineGranularity.Quarter,
            _ => TimelineGranularity.Month
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
            TimelineGranularity.Hour => "hour",
            TimelineGranularity.Day => "day",
            TimelineGranularity.Weekly => "week",
            TimelineGranularity.Quarter => "quarter",
            _ => "month"
        };
    }

    private static DateTime GetWeekStart(DateTime normalized)
    {
        return normalized.Date.AddDays(-((int)normalized.DayOfWeek + 6) % 7);
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
        Hour,
        Day,
        Weekly,
        Month,
        Quarter
    }

    private sealed record TimelineAggregateRow(
        int Year,
        int Month,
        int Day,
        int Hour,
        double TotalAmount,
        double TotalQuantity,
        double TotalWeightKg,
        int RecordCount);
}
