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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
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
        return item is null ? NotFound() : Ok(item);
    }
}
