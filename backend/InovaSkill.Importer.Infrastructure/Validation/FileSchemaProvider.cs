using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Infrastructure.Validation;

public sealed class FileSchemaProvider : IFileSchemaProvider
{
    public FileSchema GetSchema(FileType fileType)
    {
        return fileType switch
        {
            FileType.Customers => new FileSchema(FileType.Customers,
            [
                new ColumnSchema("name", true, ColumnDataType.String),
                new ColumnSchema("email", true, ColumnDataType.Email),
                new ColumnSchema("createdat", false, ColumnDataType.DateTime)
            ]),
            FileType.Products => new FileSchema(FileType.Products,
            [
                new ColumnSchema("sku", true, ColumnDataType.String),
                new ColumnSchema("name", true, ColumnDataType.String),
                new ColumnSchema("price", true, ColumnDataType.Decimal),
                new ColumnSchema("createdat", false, ColumnDataType.DateTime)
            ]),
            FileType.Orders => new FileSchema(FileType.Orders,
            [
                new ColumnSchema("ordernumber", true, ColumnDataType.String),
                new ColumnSchema("customeremail", true, ColumnDataType.Email),
                new ColumnSchema("productsku", true, ColumnDataType.String),
                new ColumnSchema("quantity", true, ColumnDataType.Int),
                new ColumnSchema("orderedat", true, ColumnDataType.DateTime)
            ]),
            _ => new FileSchema(FileType.Unknown, [])
        };
    }
}
