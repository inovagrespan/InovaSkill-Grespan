using System.Text.Json;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/template-configs")]
public sealed class TemplateConfigsController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateConfigDto>>> GetAll(CancellationToken cancellationToken)
    {
        var templates = await dbContext.PreProcessorTemplates
            .AsNoTracking()
            .Where(x => x.FileType != FileType.Unknown)
            .OrderBy(x => x.FileType)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var grouped = templates
            .GroupBy(x => x.FileType)
            .Select(g => g.First())
            .Select(Map)
            .ToList();

        return Ok(grouped);
    }

    [HttpPut]
    public async Task<ActionResult<TemplateConfigDto>> Save([FromBody] SaveTemplateConfigRequest request, CancellationToken cancellationToken)
    {
        if (request.FileType == FileType.Unknown)
        {
            return BadRequest("FileType deve ser Customers, Products ou Orders.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name é obrigatório.");
        }

        var existing = await dbContext.PreProcessorTemplates
            .FirstOrDefaultAsync(x => x.FileType == request.FileType, cancellationToken);

        if (existing is null)
        {
            existing = new PreProcessorTemplate
            {
                Code = $"simple_{request.FileType.ToString().ToLowerInvariant()}",
                FileNamePattern = request.FileType.ToString().ToLowerInvariant(),
                FileType = request.FileType,
                ColumnMappingsJson = "[]",
                ValidationRulesJson = string.Empty,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.PreProcessorTemplates.Add(existing);
        }

        existing.Name = request.Name.Trim();
        existing.IsActive = request.IsActive;
        existing.RequiredHeadersCsv = request.RequiredHeadersCsv?.Trim() ?? string.Empty;
        existing.ColumnMappingsJson = JsonSerializer.Serialize(
            (request.Aliases ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x.From) && !string.IsNullOrWhiteSpace(x.To))
                .Select(x => new TemplateAliasDto(x.From.Trim(), x.To.Trim()))
                .ToList());
        existing.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(existing));
    }

    private static TemplateConfigDto Map(PreProcessorTemplate template)
    {
        var aliases = ParseAliases(template.ColumnMappingsJson);
        return new TemplateConfigDto(
            template.Id,
            template.FileType,
            template.Name,
            template.IsActive,
            template.RequiredHeadersCsv,
            aliases);
    }

    private static IReadOnlyList<TemplateAliasDto> ParseAliases(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<TemplateAliasDto>>(json);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }
}
