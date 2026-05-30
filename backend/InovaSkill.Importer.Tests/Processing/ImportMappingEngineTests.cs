using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Mappings;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Processing.TransformRules;

namespace InovaSkill.Importer.Tests.Processing;

public class ImportMappingEngineTests
{
    [Fact]
    public void MapRow_ShouldApplyMultipleRulesInOrder()
    {
        var template = BuildTemplate(isRequired: false, defaultValue: null, includeOnlyDigits: true);
        var engine = new ImportMappingEngine(BuildRegistry());

        var row = new Dictionary<string, object?>
        {
            ["cnpj_cliente"] = " 12.345.678/0001-90 "
        };

        var result = engine.MapRow(1, row, template);

        Assert.Empty(result.Errors);
        Assert.Equal("12345678000190", result.StandardValues["CustomerId"]);
    }

    [Fact]
    public void MapRow_ShouldReturnErrorWhenRequiredColumnIsMissing()
    {
        var template = BuildTemplate(isRequired: true, defaultValue: null, includeOnlyDigits: true);
        var engine = new ImportMappingEngine(BuildRegistry());

        var result = engine.MapRow(2, new Dictionary<string, object?>(), template);

        Assert.Contains(result.Errors, x => x.Message.Contains("Coluna obrigatoria", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MapRow_ShouldUseDefaultValueWhenValueIsEmpty()
    {
        var template = BuildTemplate(isRequired: false, defaultValue: "  SEM NOME  ", includeOnlyDigits: false);
        var engine = new ImportMappingEngine(BuildRegistry());

        var row = new Dictionary<string, object?>
        {
            ["cnpj_cliente"] = ""
        };

        var result = engine.MapRow(3, row, template);

        Assert.Equal("SEM NOME", result.StandardValues["CustomerId"]);
    }

    private static ITransformRuleRegistry BuildRegistry()
    {
        return new TransformRuleRegistry([
            new TrimRule(),
            new OnlyDigitsRule(),
            new UpperCaseRule()
        ]);
    }

    private static ImportTemplate BuildTemplate(bool isRequired, string? defaultValue, bool includeOnlyDigits)
    {
        var mappingRules = new List<ColumnMappingTransformRule>
        {
            new() { Order = 1, TransformRule = new TransformRule { Code = "Trim" } },
            new() { Order = 3, TransformRule = new TransformRule { Code = "UpperCase" } }
        };
        if (includeOnlyDigits)
        {
            mappingRules.Insert(1, new ColumnMappingTransformRule { Order = 2, TransformRule = new TransformRule { Code = "OnlyDigits" } });
        }

        return new ImportTemplate
        {
            Name = "Template Teste",
            Description = "",
            RequiredHeadersCsv = "cnpj_cliente",
            ColumnMappings =
            [
                new ImportColumnMapping
                {
                    SourceColumnName = "cnpj_cliente",
                    TargetFieldName = "CustomerId",
                    IsRequired = isRequired,
                    DefaultValue = defaultValue,
                    TransformRules = mappingRules
                }
            ]
        };
    }
}
