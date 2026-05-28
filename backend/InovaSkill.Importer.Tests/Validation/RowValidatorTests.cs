using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Validation;

namespace InovaSkill.Importer.Tests.Validation;

public class RowValidatorTests
{
    private readonly RowValidator _sut = new();
    private readonly FileSchemaProvider _schemaProvider = new();

    [Fact]
    public void Validate_ReturnsValid_ForValidCustomerRow()
    {
        var schema = _schemaProvider.GetSchema(FileType.Customers);
        var row = new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice",
            ["email"] = "alice@corp.com",
            ["createdat"] = "2026-05-20"
        });

        var result = _sut.Validate(row, schema);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsErrors_ForInvalidCustomerRow()
    {
        var schema = _schemaProvider.GetSchema(FileType.Customers);
        var row = new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "",
            ["email"] = "invalid-email"
        });

        var result = _sut.Validate(row, schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Column == "name");
        Assert.Contains(result.Errors, e => e.Column == "email");
    }
}
