using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/preprocessor-templates")]
public sealed class PreProcessorTemplatesController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PreProcessorTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var templates = await dbContext.PreProcessorTemplates
            .AsNoTracking()
            .Include(x => x.Rules)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return Ok(templates.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PreProcessorTemplateDto>> Create([FromBody] UpsertPreProcessorTemplateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Code is required.");
        }

        var exists = await dbContext.PreProcessorTemplates.AnyAsync(x => x.Code == request.Code, cancellationToken);
        if (exists)
        {
            return Conflict($"Template code '{request.Code}' already exists.");
        }

        var entity = new PreProcessorTemplate();
        Apply(entity, request);
        dbContext.PreProcessorTemplates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PreProcessorTemplateDto>> Update(long id, [FromBody] UpsertPreProcessorTemplateRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PreProcessorTemplates
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var duplicate = await dbContext.PreProcessorTemplates
            .AnyAsync(x => x.Id != id && x.Code == request.Code, cancellationToken);
        if (duplicate)
        {
            return Conflict($"Template code '{request.Code}' already exists.");
        }

        Apply(entity, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity));
    }

    private static PreProcessorTemplateDto Map(PreProcessorTemplate template)
    {
        var rules = template.Rules
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new PreProcessorTemplateRuleDto(x.Id, x.Name, x.RuleType, x.IsEnabled, x.ConfigJson, x.SortOrder))
            .ToList();

        return new PreProcessorTemplateDto(
            template.Id,
            template.Code,
            template.Name,
            template.IsActive,
            template.FileType,
            template.FileNamePattern,
            template.RequiredHeadersCsv,
            template.ColumnMappingsJson,
            template.ValidationRulesJson,
            rules);
    }

    private static void Apply(PreProcessorTemplate target, UpsertPreProcessorTemplateRequest request)
    {
        target.Code = request.Code.Trim();
        target.Name = request.Name.Trim();
        target.IsActive = request.IsActive;
        target.FileType = request.FileType;
        target.FileNamePattern = request.FileNamePattern?.Trim() ?? string.Empty;
        target.RequiredHeadersCsv = request.RequiredHeadersCsv?.Trim() ?? string.Empty;
        target.ColumnMappingsJson = request.ColumnMappingsJson?.Trim() ?? string.Empty;
        target.ValidationRulesJson = request.ValidationRulesJson?.Trim() ?? string.Empty;
        target.UpdatedAt = DateTime.UtcNow;

        var incoming = request.Rules ?? [];
        target.Rules.Clear();
        foreach (var item in incoming)
        {
            target.Rules.Add(new PreProcessorTemplateRule
            {
                Name = item.Name?.Trim() ?? string.Empty,
                RuleType = item.RuleType?.Trim() ?? string.Empty,
                IsEnabled = item.IsEnabled,
                ConfigJson = string.IsNullOrWhiteSpace(item.ConfigJson) ? "{}" : item.ConfigJson.Trim(),
                SortOrder = item.SortOrder
            });
        }
    }
}

