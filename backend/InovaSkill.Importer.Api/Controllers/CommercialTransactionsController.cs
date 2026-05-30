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
    [HttpGet]
    public async Task<ActionResult<PagedResult<CommercialTransactionDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? documentNumber = null,
        [FromQuery] string? customerCode = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? city = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = dbContext.CommercialTransactions.AsNoTracking().AsQueryable();

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

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.TransactionDate >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.TransactionDate < toExclusive);
        }

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
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = dbContext.CommercialTransactions.AsNoTracking().AsQueryable();

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

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.TransactionDate >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.TransactionDate < toExclusive);
        }

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

        var normalizedGranularity = granularity.Trim().ToLowerInvariant();
        var resolvedGranularity = normalizedGranularity switch
        {
            "daily" => SalesSummaryGranularity.Daily,
            "monthly" => SalesSummaryGranularity.Monthly,
            _ => SalesSummaryGranularity.Weekly
        };

        var normalizedSortBy = sortBy.Trim().ToLowerInvariant();
        var resolvedSortBy = normalizedSortBy switch
        {
            "amount" => SalesSummarySortBy.Amount,
            "weight" => SalesSummarySortBy.Weight,
            "quantity" => SalesSummarySortBy.Quantity,
            _ => SalesSummarySortBy.Growth
        };

        var summary = SalesCompanySummaryCalculator.Build(rows, resolvedGranularity, resolvedSortBy, referenceDate);
        var pagedItems = summary.Items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new CommercialTransactionSummaryResponseDto(
            page,
            pageSize,
            summary.TotalCompanies,
            resolvedGranularity switch
            {
                SalesSummaryGranularity.Daily => "daily",
                SalesSummaryGranularity.Monthly => "monthly",
                _ => "weekly"
            },
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
}
