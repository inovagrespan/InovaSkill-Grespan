using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Validation;

namespace InovaSkill.Importer.Tests.Processing;

public class CommercialTransactionReducedSampleTests
{
    [Fact]
    public void ReducedSample_ShouldNormalizeDateAndAcceptDecimalQuantity()
    {
        var row = new ImportedRow(153, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documento"] = "000424826",
            ["data"] = "20250701",
            ["quantidade"] = "0.23",
            ["total"] = "83.00",
            ["tipo"] = "N"
        });

        var transformRules = new List<PreProcessorTemplateRule>
        {
            new() { Name = "normalize_date", RuleType = "normalize_date", IsEnabled = true, SortOrder = 10, ConfigJson = "{\"column\":\"data\",\"target\":\"transactiondate\",\"formats\":[\"yyyyMMdd\"],\"outputFormat\":\"yyyy-MM-dd\"}" },
            new() { Name = "map_quantity", RuleType = "map_column", IsEnabled = true, SortOrder = 20, ConfigJson = "{\"from\":\"quantidade\",\"to\":\"quantity\",\"overwrite\":false}" },
            new() { Name = "map_tipo", RuleType = "map_column", IsEnabled = true, SortOrder = 30, ConfigJson = "{\"from\":\"tipo\",\"to\":\"transactiontype\",\"overwrite\":false}" },
            new() { Name = "map_documento", RuleType = "map_column", IsEnabled = true, SortOrder = 40, ConfigJson = "{\"from\":\"documento\",\"to\":\"documentnumber\",\"overwrite\":false}" },
            new() { Name = "map_total", RuleType = "map_column", IsEnabled = true, SortOrder = 50, ConfigJson = "{\"from\":\"total\",\"to\":\"totalamount\",\"overwrite\":false}" }
        };

        var validationRules = new List<PreProcessorTemplateRule>
        {
            new() { Name = "validate_datetime", RuleType = "validate_datetime", IsEnabled = true, SortOrder = 100, ConfigJson = "{\"column\":\"transactiondate\",\"message\":\"transactiondate invalido\"}" },
            new() { Name = "validate_decimal_quantity", RuleType = "validate_decimal", IsEnabled = true, SortOrder = 110, ConfigJson = "{\"column\":\"quantity\",\"message\":\"quantity invalido\"}" }
        };

        var engine = new PreProcessorRuleEngine();
        var transformed = engine.Execute(row, transformRules);
        var validated = engine.Execute(transformed.Row, validationRules);

        Assert.Empty(transformed.Errors);
        Assert.Empty(validated.Errors);
        Assert.Equal("2025-07-01", transformed.Row.Get("transactiondate"));
        Assert.Equal("0.23", transformed.Row.Get("quantity"));
        Assert.Equal("N", transformed.Row.Get("transactiontype"));

        var schema = new FileSchemaProvider().GetSchema(ImportFileTypeCodes.SalesInvoice);
        var rowValidator = new RowValidator();
        var schemaValidation = rowValidator.Validate(transformed.Row, schema);
        Assert.DoesNotContain(schemaValidation.Errors, e => e.Column.Equals("quantity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(schemaValidation.Errors, e => e.Column.Equals("transactiondate", StringComparison.OrdinalIgnoreCase));
    }
}
