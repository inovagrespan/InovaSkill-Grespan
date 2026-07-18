using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/fiscal-documents")]
public sealed class FiscalDocumentsController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;
    private const int DefaultReturnRatePeriodDays = 30;
    private const int MinimumReturnRatePeriodDays = 1;
    private const int MaximumReturnRatePeriodDays = 365;
    private const int PercentScale = 100;
    private const int PercentDecimalPlaces = 1;

    [HttpGet]
    public async Task<ActionResult> List(int page = 1, int pageSize = DefaultPageSize, string? search = null,
        FiscalMovementCategory? operationCategory = null, DateOnly? dateFrom = null, DateOnly? dateTo = null,
        Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var query = dbContext.FiscalDocuments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(x => x.DocumentNumber.ToUpper().Contains(term) ||
                x.CustomerNameAtIssue.ToUpper().Contains(term) || x.CustomerCodeAtIssue.ToUpper().Contains(term) ||
                x.CityNameAtIssue.ToUpper().Contains(term));
        }
        if (operationCategory.HasValue) query = query.Where(x => x.MovementCategory == operationCategory);
        if (dateFrom.HasValue) query = query.Where(x => x.IssueDate >= dateFrom);
        if (dateTo.HasValue) query = query.Where(x => x.IssueDate <= dateTo);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.DocumentNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => new {
                x.Id, x.IssueDate, x.DocumentNumber, x.Series, x.CustomerId, x.CustomerNameAtIssue,
                x.CustomerCodeAtIssue, x.BranchCodeAtIssue, x.CityNameAtIssue, x.StateCodeAtIssue,
                operationCategory = x.MovementCategory.ToString(), x.OperationDescription,
                itemCount = x.Items.Count, grossWeightKg = x.Items.Sum(item => item.GrossWeightKg)
            }).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("return-rate")]
    public async Task<ActionResult> ReturnRate(
        [FromQuery] int periodDays = DefaultReturnRatePeriodDays,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        periodDays = Math.Clamp(periodDays, MinimumReturnRatePeriodDays, MaximumReturnRatePeriodDays);
        var referenceDate = dateTo ?? await dbContext.FiscalDocuments.AsNoTracking()
            .MaxAsync(document => (DateOnly?)document.IssueDate, cancellationToken);

        if (!referenceDate.HasValue)
            return Ok(new
            {
                periodDays,
                dateFrom = (DateOnly?)null,
                dateTo = (DateOnly?)null,
                salesWeightKg = 0m,
                returnWeightKg = 0m,
                returnRatePercent = 0m
            });

        var dateFrom = referenceDate.Value.AddDays(-(periodDays - 1));
        var weights = await dbContext.FiscalDocumentItems.AsNoTracking()
            .Where(item =>
                item.FiscalDocument!.IssueDate >= dateFrom &&
                item.FiscalDocument.IssueDate <= referenceDate.Value &&
                (item.FiscalDocument.MovementCategory == FiscalMovementCategory.Sale ||
                    item.FiscalDocument.MovementCategory == FiscalMovementCategory.Return))
            .GroupBy(item => item.FiscalDocument!.MovementCategory)
            .Select(grouped => new
            {
                category = grouped.Key,
                grossWeightKg = grouped.Sum(item => item.GrossWeightKg)
            })
            .ToListAsync(cancellationToken);

        var salesWeightKg = weights.SingleOrDefault(item => item.category == FiscalMovementCategory.Sale)?.grossWeightKg ?? 0m;
        var returnWeightKg = weights.SingleOrDefault(item => item.category == FiscalMovementCategory.Return)?.grossWeightKg ?? 0m;
        var returnRatePercent = salesWeightKg <= 0
            ? 0m
            : Math.Round(returnWeightKg / salesWeightKg * PercentScale, PercentDecimalPlaces);

        return Ok(new
        {
            periodDays,
            dateFrom,
            dateTo = referenceDate.Value,
            salesWeightKg,
            returnWeightKg,
            returnRatePercent
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var documentTotals = dbContext.FiscalDocuments.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                CalculatedTotalAmount = x.Items.Sum(item =>
                    item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0)
            });

        var item = await dbContext.FiscalDocuments.AsNoTracking().Where(x => x.Id == id).Select(x => new {
            x.Id, x.IssueDate, x.DocumentNumber, x.Series, x.DocumentType, x.MovementType, x.CustomerId,
            x.CustomerNameAtIssue, x.CustomerCodeAtIssue, x.BranchCodeAtIssue, x.CityNameAtIssue,
            x.StateCodeAtIssue, x.OperationCode, x.OperationDescription,
            operationCategory = x.MovementCategory.ToString(), x.OriginalDocumentNumber,
            itemCount = x.Items.Count, grossWeightKg = x.Items.Sum(item => item.GrossWeightKg),
            totalQuantity = x.Items.Sum(item => item.Quantity),
            calculatedTotalAmount = x.Items.Sum(item =>
                item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0),
            items = x.Items.OrderBy(item => item.ItemNumber).Select(item => new {
                item.Id, item.ItemNumber, item.ProductCode, item.ProductDescription,
                item.ProductGroupCode, item.ProductGroupDescription, item.Quantity,
                item.GrossWeightKg, item.UnitValue,
                calculatedAmount = item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0
            })
        }).SingleOrDefaultAsync(cancellationToken);
        if (item is null) return NotFound();

        var customerAverageTicket = item.CustomerId is null
            ? null
            : await dbContext.FiscalDocuments.AsNoTracking()
                .Where(x => x.Id != item.Id && x.CustomerId == item.CustomerId &&
                    x.IssueDate < item.IssueDate &&
                    x.MovementCategory == FiscalMovementCategory.Sale)
                .Join(documentTotals, document => document.Id, total => total.Id,
                    (document, total) => total.CalculatedTotalAmount)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Count = group.Count(),
                    Average = group.Average()
                })
                .SingleOrDefaultAsync(cancellationToken);

        var commercialQuality = CommercialSaleQualityCalculator.Calculate(new CommercialSaleQualityInput(
            item.calculatedTotalAmount,
            customerAverageTicket?.Average,
            customerAverageTicket?.Count ?? 0,
            item.operationCategory == FiscalMovementCategory.Sale.ToString()));

        return Ok(new {
            item.Id, item.IssueDate, item.DocumentNumber, item.Series, item.DocumentType, item.MovementType,
            item.CustomerId, item.CustomerNameAtIssue, item.CustomerCodeAtIssue, item.BranchCodeAtIssue,
            item.CityNameAtIssue, item.StateCodeAtIssue, item.OperationCode, item.OperationDescription,
            item.operationCategory, item.OriginalDocumentNumber, item.itemCount, item.grossWeightKg,
            item.totalQuantity, item.calculatedTotalAmount, commercialQuality, item.items
        });
    }
}
