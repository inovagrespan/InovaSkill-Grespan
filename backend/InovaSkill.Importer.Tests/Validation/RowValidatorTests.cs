using InovaSkill.Importer.Domain.Entities;
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
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.Customers);
        var row = new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customercode"] = "C-001",
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
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.Customers);
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

    [Fact]
    public void Validate_ReturnsError_WhenSalesDecimalExceedsDatabasePrecision()
    {
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.SalesInvoice);
        var row = BuildValidSalesInvoiceRow();
        row["unitprice"] = "10000000000000000";

        var result = _sut.Validate(new ImportedRow(2, row), schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Column == "unitprice" &&
            e.Message.Contains("numeric(18,2)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenCalculatedSalesTotalExceedsDatabasePrecision()
    {
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.SalesInvoice);
        var row = BuildValidSalesInvoiceRow();
        row["quantity"] = "999999999999999.999";
        row["unitprice"] = "11.00";
        row["totalamount"] = "1.00";

        var result = _sut.Validate(new ImportedRow(2, row), schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Column == "totalamount" &&
            e.Message.Contains("Calculated total amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AcceptsSalesDecimalsAtDatabasePrecisionLimit()
    {
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.SalesInvoice);
        var row = BuildValidSalesInvoiceRow();
        row["quantity"] = "999999999999999.999";
        row["unitprice"] = "1.00";
        row["grossweightkg"] = "999999999999999.999";

        var result = _sut.Validate(new ImportedRow(2, row), schema);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenFinancialEntryQuantityIsNotInteger()
    {
        var schema = _schemaProvider.GetSchema(ImportFileTypeCodes.FinancialEntry);
        var row = new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = "P-001",
            ["customeremail"] = "cliente@corp.com",
            ["productsku"] = "SKU-1",
            ["quantity"] = "1.5",
            ["orderedat"] = "2026-05-20"
        });

        var result = _sut.Validate(row, schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Column == "quantity");
    }

    private static Dictionary<string, string> BuildValidSalesInvoiceRow()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "NF-001",
            ["transactiondate"] = "2026-05-20",
            ["customercode"] = "C-001",
            ["customername"] = "Cliente Teste",
            ["productcode"] = "P-001",
            ["productdescription"] = "Produto Teste",
            ["quantity"] = "2.000",
            ["unitprice"] = "10.00",
            ["totalamount"] = "20.00",
            ["transactiontype"] = "N",
            ["city"] = "Sao Paulo",
            ["productgroup"] = "Grupo",
            ["grossweightkg"] = "1.000"
        };
    }
}
