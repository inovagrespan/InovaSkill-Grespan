using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class PreProcessorDateValidationTests
{
    [Fact]
    public void ValidateDateTime_ShouldAcceptBrazilianDateTimeWithTime()
    {
        var engine = new PreProcessorRuleEngine();
        var row = new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transactiondate"] = "31/07/2025 00:00:00"
        });
        var rules = new[]
        {
            Rule("validate_datetime", "{\"column\":\"transactiondate\",\"message\":\"transactiondate inválido\"}")
        };

        var result = engine.Execute(row, rules);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateDateTime_ShouldRejectInvalidCalendarDate()
    {
        var engine = new PreProcessorRuleEngine();
        var row = new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transactiondate"] = "31/13/2025"
        });
        var rules = new[]
        {
            Rule("validate_datetime", "{\"column\":\"transactiondate\",\"message\":\"transactiondate inválido\"}")
        };

        var result = engine.Execute(row, rules);

        var error = Assert.Single(result.Errors);
        Assert.Equal("transactiondate", error.Column);
        Assert.Equal("transactiondate inválido", error.Message);
    }

    [Fact]
    public void NormalizeDate_ShouldFallbackToSupportedImportFormatsWhenConfiguredFormatsMissTimePart()
    {
        var engine = new PreProcessorRuleEngine();
        var row = new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["data"] = "31/07/2025 00:00:00"
        });
        var rules = new[]
        {
            Rule("normalize_date", "{\"column\":\"data\",\"target\":\"transactiondate\",\"formats\":[\"dd/MM/yyyy\"],\"outputFormat\":\"yyyy-MM-dd\"}")
        };

        var result = engine.Execute(row, rules);

        Assert.Empty(result.Errors);
        Assert.Equal("2025-07-31", result.Row.Get("transactiondate"));
    }

    private static PreProcessorTemplateRule Rule(string type, string configJson)
    {
        return new PreProcessorTemplateRule
        {
            Name = type,
            RuleType = type,
            IsEnabled = true,
            SortOrder = 1,
            ConfigJson = configJson
        };
    }
}
