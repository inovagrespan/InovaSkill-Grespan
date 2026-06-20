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

    [HttpGet("invoices")]
    public async Task<ActionResult<CommercialInvoiceSummaryResponseDto>> GetInvoices(
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

        var invoiceRows = (await BuildInvoiceAggregateRowsAsync(query, cancellationToken))
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.DocumentNumber)
            .ToList();

        var invoices = invoiceRows
            .Select(x => new CommercialInvoiceSummaryDto(
                x.DocumentNumber,
                x.Date,
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.TransactionType,
                (decimal)x.TotalAmount,
                (decimal)x.TotalQuantity,
                (decimal)x.TotalWeightKg,
                x.TotalItems))
            .ToList();

        var totalItems = invoices.Count;
        var pagedItems = invoices
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new CommercialInvoiceSummaryResponseDto(
            page,
            pageSize,
            totalItems,
            invoices.Sum(x => x.TotalAmount),
            invoices.Sum(x => x.TotalQuantity),
            invoices.Sum(x => x.TotalWeightKg),
            pagedItems));
    }

    [HttpGet("invoice-analytics")]
    public async Task<ActionResult<CommercialInvoiceAnalyticsResponseDto>> GetInvoiceAnalytics(
        [FromQuery] string granularity = "month",
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
        var resolvedGranularity = ResolveInvoiceAnalyticsGranularity(granularity);
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

        var invoiceRows = (await BuildInvoiceAggregateRowsAsync(query, cancellationToken))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.DocumentNumber)
            .ToList();

        var summary = new CommercialInvoiceAnalyticsSummaryDto(
            invoiceRows.Count,
            invoiceRows.Sum(x => (decimal)x.TotalAmount),
            invoiceRows.Sum(x => (decimal)x.TotalWeightKg),
            invoiceRows
                .Select(x => ((x.CustomerCode ?? string.Empty).Trim(), (x.CustomerName ?? string.Empty).Trim()))
                .Where(x => x.Item2 != string.Empty || x.Item1 != string.Empty)
                .Distinct()
                .Count(),
            invoiceRows.Sum(x => x.TotalItems),
            invoiceRows.Sum(x => (decimal)x.TotalQuantity));

        var trend = BuildInvoiceAnalyticsTrend(invoiceRows, resolvedGranularity)
            .Select(x => new CommercialInvoiceAnalyticsTrendPointDto(
                x.PeriodStart,
                x.InvoiceCount,
                x.TotalAmount,
                x.TotalWeightKg))
            .ToList();

        var ranking = invoiceRows
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(group => new CommercialInvoiceAnalyticsRankingItemDto(
                group.Key.CustomerCode,
                group.Key.CustomerName,
                group.Sum(x => (decimal)x.TotalAmount),
                group.Count(),
                group.Sum(x => x.TotalItems),
                group.Sum(x => (decimal)x.TotalWeightKg)))
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.CustomerName)
            .ToList();

        return Ok(new CommercialInvoiceAnalyticsResponseDto(
            ToInvoiceAnalyticsGranularityName(resolvedGranularity),
            summary,
            trend,
            ranking));
    }

    [HttpGet("invoices/{documentNumber}")]
    public async Task<ActionResult<CommercialInvoiceDetailsDto>> GetInvoiceDetails(
        [FromRoute] string documentNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedDocumentNumber = documentNumber.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDocumentNumber))
        {
            return BadRequest("Documento inválido.");
        }

        var items = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.DocumentNumber.ToUpper() == normalizedDocumentNumber.ToUpper())
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.ProductCode)
            .ThenBy(x => x.Id)
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

        if (items.Count == 0)
        {
            return NotFound();
        }

        var firstItem = items[0];

        return Ok(new CommercialInvoiceDetailsDto(
            firstItem.DocumentNumber,
            firstItem.TransactionDate.Date,
            firstItem.CustomerCode,
            firstItem.CustomerName,
            firstItem.City,
            firstItem.TransactionType,
            items.Sum(x => x.TotalAmount),
            items.Sum(x => x.Quantity),
            items.Sum(x => x.GrossWeightKg),
            items.Count,
            items));
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

    private static async Task<List<InvoiceAggregateRow>> BuildInvoiceAggregateRowsAsync(
        IQueryable<Domain.Entities.CommercialTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(x => new
            {
                x.DocumentNumber,
                Date = x.TransactionDate.Date,
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.TransactionType
            })
            .Select(group => new
            {
                group.Key.DocumentNumber,
                group.Key.Date,
                group.Key.CustomerCode,
                group.Key.CustomerName,
                group.Key.City,
                group.Key.TransactionType,
                TotalAmount = group.Sum(x => (double)x.TotalAmount),
                TotalQuantity = group.Sum(x => (double)x.Quantity),
                TotalWeightKg = group.Sum(x => (double)x.GrossWeightKg),
                TotalItems = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new InvoiceAggregateRow(
                row.DocumentNumber,
                row.Date,
                row.CustomerCode,
                row.CustomerName,
                row.City,
                row.TransactionType,
                row.TotalAmount,
                row.TotalQuantity,
                row.TotalWeightKg,
                row.TotalItems))
            .ToList();
    }

    private static IReadOnlyList<InvoiceAnalyticsTrendRow> BuildInvoiceAnalyticsTrend(
        IReadOnlyList<InvoiceAggregateRow> invoiceRows,
        InvoiceAnalyticsGranularity granularity)
    {
        return invoiceRows
            .GroupBy(x => ResolveInvoiceAnalyticsPeriodStart(x.Date, granularity))
            .OrderBy(x => x.Key)
            .Select(group => new InvoiceAnalyticsTrendRow(
                group.Key,
                group.Count(),
                group.Sum(x => (decimal)x.TotalAmount),
                group.Sum(x => (decimal)x.TotalWeightKg)))
            .ToList();
    }

    private static DateTime ResolveInvoiceAnalyticsPeriodStart(DateTime date, InvoiceAnalyticsGranularity granularity)
    {
        var normalizedDate = NormalizeToUtc(date).Date;
        return granularity switch
        {
            InvoiceAnalyticsGranularity.Day => normalizedDate,
            InvoiceAnalyticsGranularity.Week => GetWeekStart(normalizedDate),
            _ => new DateTime(normalizedDate.Year, normalizedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };
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

    private static InvoiceAnalyticsGranularity ResolveInvoiceAnalyticsGranularity(string granularity)
    {
        return granularity.Trim().ToLowerInvariant() switch
        {
            "daily" or "day" => InvoiceAnalyticsGranularity.Day,
            "weekly" or "week" => InvoiceAnalyticsGranularity.Week,
            _ => InvoiceAnalyticsGranularity.Month
        };
    }

    private static string ToInvoiceAnalyticsGranularityName(InvoiceAnalyticsGranularity granularity)
    {
        return granularity switch
        {
            InvoiceAnalyticsGranularity.Day => "day",
            InvoiceAnalyticsGranularity.Week => "week",
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

    private enum InvoiceAnalyticsGranularity
    {
        Day,
        Week,
        Month
    }

    private sealed record InvoiceAggregateRow(
        string DocumentNumber,
        DateTime Date,
        string CustomerCode,
        string CustomerName,
        string City,
        string TransactionType,
        double TotalAmount,
        double TotalQuantity,
        double TotalWeightKg,
        int TotalItems);

    private sealed record InvoiceAnalyticsTrendRow(
        DateTime PeriodStart,
        int InvoiceCount,
        decimal TotalAmount,
        decimal TotalWeightKg);

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
