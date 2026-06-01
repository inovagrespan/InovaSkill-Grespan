using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Parsing;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

[ApiController]
[Route("api/import-templates")]
public sealed class ImportTemplatesController(ImportDbContext dbContext) : ControllerBase
{
    private const long MaxTemplateSampleBytes = 524_288_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("file-types")]
    public async Task<ActionResult<IReadOnlyList<ImportTemplateFileTypeDto>>> GetFileTypes(CancellationToken cancellationToken)
    {
        var fileTypes = await dbContext.ImportFileTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ImportTemplateFileTypeDto(
                x.Id.ToString(),
                x.Code,
                x.Name,
                x.Description,
                x.AllowedExtensions))
            .ToListAsync(cancellationToken);

        return Ok(fileTypes);
    }

    [HttpGet("file-types/{id}/fields")]
    public async Task<ActionResult<IReadOnlyList<ImportTemplateTargetFieldDto>>> GetTargetFields(string id, CancellationToken cancellationToken)
    {
        var fileType = await ResolveFileTypeAsync(id, cancellationToken);
        return fileType is null ? NotFound() : Ok(GetTargetFieldsByCode(fileType.Code));
    }

    [HttpGet("transform-rules")]
    public async Task<ActionResult<IReadOnlyList<TransformRuleDto>>> GetTransformRules(CancellationToken cancellationToken)
    {
        var rules = await dbContext.TransformRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new TransformRuleDto(
                x.Id.ToString(),
                x.Code,
                x.Name,
                x.Description,
                RequiresParameters(x.Code)))
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportTemplateDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var template = await dbContext.ImportTemplates
            .AsNoTracking()
            .Include(x => x.ColumnMappings)
                .ThenInclude(x => x.TransformRules)
                    .ThenInclude(x => x.TransformRule)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return template is null ? NotFound() : Ok(Map(template));
    }

    [HttpPost("extract-headers")]
    [RequestSizeLimit(MaxTemplateSampleBytes)]
    public async Task<ActionResult<SpreadsheetHeaderPreviewDto>> ExtractHeaders([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Arquivo de exemplo é obrigatório.");
        }

        await using var stream = file.OpenReadStream();
        var sample = await SpreadsheetSampleExtractor.ExtractAsync(file.FileName, stream, cancellationToken: cancellationToken);
        return Ok(new SpreadsheetHeaderPreviewDto(sample.Headers, sample.PreviewRows));
    }

    [HttpPost]
    public async Task<ActionResult<ImportTemplateDto>> Create([FromBody] UpsertImportTemplateRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequest(validationError);
        }

        var fileType = await ResolveFileTypeAsync(request.ImportFileTypeId, cancellationToken);
        if (fileType is null)
        {
            return BadRequest("Tipo de arquivo inválido.");
        }

        var template = new ImportTemplate
        {
            ImportFileTypeId = fileType.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = true,
            FileNamePattern = string.Empty,
            RequiredHeadersCsv = BuildRequiredHeadersCsv(request.ColumnMappings),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await ApplyMappingsAsync(template, request.ColumnMappings, cancellationToken);
        dbContext.ImportTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(template));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ImportTemplateDto>> Update(long id, [FromBody] UpsertImportTemplateRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequest(validationError);
        }

        var template = await dbContext.ImportTemplates
            .Include(x => x.ColumnMappings)
                .ThenInclude(x => x.TransformRules)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (template is null)
        {
            return NotFound();
        }

        var fileType = await ResolveFileTypeAsync(request.ImportFileTypeId, cancellationToken);
        if (fileType is null)
        {
            return BadRequest("Tipo de arquivo inválido.");
        }

        template.ImportFileTypeId = fileType.Id;
        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim() ?? string.Empty;
        template.RequiredHeadersCsv = BuildRequiredHeadersCsv(request.ColumnMappings);
        template.UpdatedAt = DateTime.UtcNow;
        template.ColumnMappings.Clear();

        await ApplyMappingsAsync(template, request.ColumnMappings, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(template));
    }

    private async Task ApplyMappingsAsync(
        ImportTemplate template,
        IReadOnlyList<ImportTemplateColumnMappingDto> mappings,
        CancellationToken cancellationToken)
    {
        var rules = await dbContext.TransformRules
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var rulesById = rules.ToDictionary(x => x.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var rulesByCode = rules.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings.Where(x => !string.IsNullOrWhiteSpace(x.SourceColumnName)))
        {
            var columnMapping = new ImportColumnMapping
            {
                SourceColumnName = mapping.SourceColumnName.Trim(),
                TargetFieldName = mapping.TargetFieldName.Trim(),
                IsRequired = mapping.IsRequired,
                DefaultValue = string.IsNullOrWhiteSpace(mapping.DefaultValue) ? null : mapping.DefaultValue.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var rule in mapping.TransformRules.OrderBy(x => x.Order))
            {
                if (!rulesById.TryGetValue(rule.TransformRuleId, out var transformRule) &&
                    !rulesByCode.TryGetValue(rule.TransformRuleId, out transformRule))
                {
                    throw new InvalidOperationException($"Regra de transformação inválida: {rule.TransformRuleId}.");
                }

                columnMapping.TransformRules.Add(new ColumnMappingTransformRule
                {
                    TransformRuleId = transformRule.Id,
                    Order = rule.Order,
                    ParametersJson = rule.ParametersJson is null ? null : JsonSerializer.Serialize(rule.ParametersJson, JsonOptions)
                });
            }

            template.ColumnMappings.Add(columnMapping);
        }
    }

    private async Task<ImportFileType?> ResolveFileTypeAsync(string idOrCode, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrCode, out var id))
        {
            return await dbContext.ImportFileTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        return await dbContext.ImportFileTypes.FirstOrDefaultAsync(x => x.Code == idOrCode, cancellationToken);
    }

    private static ImportTemplateDto Map(ImportTemplate template)
    {
        return new ImportTemplateDto(
            template.Id.ToString(),
            template.Name,
            template.Description,
            template.ImportFileTypeId.ToString(),
            template.IsActive,
            template.ColumnMappings
                .OrderBy(x => x.Id)
                .Select(x => new ImportTemplateColumnMappingDto(
                    x.SourceColumnName,
                    x.TargetFieldName,
                    x.IsRequired,
                    x.DefaultValue,
                    x.TransformRules
                        .OrderBy(r => r.Order)
                        .ThenBy(r => r.Id)
                        .Select(r => new ImportTemplateRuleDto(
                            r.TransformRuleId.ToString(),
                            r.Order,
                            ParseParameters(r.ParametersJson)))
                        .ToList()))
                .ToList());
    }

    private static object? ParseParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(parametersJson, JsonOptions);
    }

    private static string ValidateRequest(UpsertImportTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Nome do template é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(request.ImportFileTypeId))
        {
            return "Tipo de arquivo é obrigatório.";
        }

        var mapped = request.ColumnMappings.Where(x => !string.IsNullOrWhiteSpace(x.SourceColumnName)).ToList();
        if (mapped.Count == 0)
        {
            return "Mapeie pelo menos uma coluna.";
        }

        var duplicateTargets = mapped
            .GroupBy(x => x.TargetFieldName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        return duplicateTargets.Count > 0
            ? $"Campos internos duplicados: {string.Join(", ", duplicateTargets)}."
            : string.Empty;
    }

    private static string BuildRequiredHeadersCsv(IReadOnlyList<ImportTemplateColumnMappingDto> mappings)
    {
        return string.Join(
            ",",
            mappings
                .Where(x => x.IsRequired && !string.IsNullOrWhiteSpace(x.SourceColumnName))
                .Select(x => x.SourceColumnName.Trim()));
    }

    private static bool RequiresParameters(string code)
    {
        return code is "BrazilianCurrency" or "BrazilianDate";
    }

    private static IReadOnlyList<ImportTemplateTargetFieldDto> GetTargetFieldsByCode(string code)
    {
        return code.ToUpperInvariant() switch
        {
            ImportFileTypeCodes.CustomerList =>
            [
                Field("customercode", "Código do Cliente", true, "text", "Identificador único do cliente."),
                Field("name", "Nome", true, "text", "Nome do cliente."),
                Field("email", "E-mail", false, "text", "E-mail de contato.")
            ],
            ImportFileTypeCodes.ProductList =>
            [
                Field("sku", "SKU", true, "text", "Código do produto."),
                Field("name", "Nome", true, "text", "Nome do produto."),
                Field("price", "Preço", false, "currency", "Preço unitário.")
            ],
            ImportFileTypeCodes.FinancialEntry =>
            [
                Field("ordernumber", "Número do Pedido", true, "text", "Identificador do pedido."),
                Field("customeremail", "E-mail do Cliente", true, "text", "Cliente vinculado."),
                Field("productsku", "SKU do Produto", true, "text", "Produto vinculado."),
                Field("quantity", "Quantidade", true, "number", "Quantidade do item."),
                Field("orderdate", "Data do Pedido", false, "date", "Data do pedido.")
            ],
            ImportFileTypeCodes.SalesInvoice =>
            [
                Field("documentnumber", "Documento", true, "text", "Número do documento fiscal."),
                Field("transactiondate", "Data da Venda", true, "date", "Data da venda."),
                Field("customercode", "Cliente", true, "text", "Código do cliente."),
                Field("customername", "Nome do Cliente", true, "text", "Nome do cliente."),
                Field("productcode", "Produto", true, "text", "Código do produto."),
                Field("productdescription", "Descrição do Produto", false, "text", "Descrição do produto."),
                Field("quantity", "Quantidade", true, "number", "Quantidade vendida."),
                Field("unitprice", "Valor Unitário", false, "currency", "Valor unitário."),
                Field("totalamount", "Total", false, "currency", "Valor total."),
                Field("transactiontype", "Tipo", false, "text", "Tipo da operação."),
                Field("city", "Cidade", false, "text", "Cidade da venda."),
                Field("productgroup", "Grupo do Produto", false, "text", "Grupo do produto."),
                Field("grossweightkg", "Peso Bruto (Kg)", false, "number", "Peso bruto.")
            ],
            _ => []
        };
    }

    private static ImportTemplateTargetFieldDto Field(
        string name,
        string displayName,
        bool required,
        string dataType,
        string description)
    {
        return new ImportTemplateTargetFieldDto(name, displayName, required, dataType, description);
    }
}
