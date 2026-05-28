using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Validation;

namespace InovaSkill.Importer.Tests.Validation;

public class FileSchemaProviderTests
{
    private readonly FileSchemaProvider _sut = new();

    [Theory]
    [InlineData(FileType.Customers)]
    [InlineData(FileType.Orders)]
    [InlineData(FileType.Products)]
    public void GetSchema_ReturnsExpectedSchema(FileType type)
    {
        var schema = _sut.GetSchema(type);

        Assert.Equal(type, schema.FileType);
        Assert.NotEmpty(schema.Columns);
    }
}
