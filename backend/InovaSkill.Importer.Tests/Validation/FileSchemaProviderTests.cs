using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Validation;

namespace InovaSkill.Importer.Tests.Validation;

public class FileSchemaProviderTests
{
    private readonly FileSchemaProvider _sut = new();

    [Theory]
    [InlineData(ImportFileTypeCodes.CustomerList)]
    [InlineData(ImportFileTypeCodes.FinancialEntry)]
    [InlineData(ImportFileTypeCodes.ProductList)]
    [InlineData(ImportFileTypeCodes.SalesInvoice)]
    public void GetSchema_ReturnsExpectedSchema(string code)
    {
        var schema = _sut.GetSchema(code);

        Assert.Equal(code, schema.ImportFileTypeCode);
        Assert.NotEmpty(schema.Columns);
    }
}
