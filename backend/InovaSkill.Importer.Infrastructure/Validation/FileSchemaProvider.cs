using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Infrastructure.Validation;

public sealed class FileSchemaProvider : IFileSchemaProvider
{
    public FileSchema GetSchema(string importFileTypeCode)
    {
        var code = importFileTypeCode?.Trim()?.ToUpperInvariant() ?? string.Empty;
        return code switch
        {
            var x when x == ImportFileTypeCodes.Customers => new FileSchema(ImportFileTypeCodes.Customers,
            [
                new ColumnSchema("customercode", true, ColumnDataType.String),
                new ColumnSchema("name", true, ColumnDataType.String),
                new ColumnSchema("email", false, ColumnDataType.Email),
                new ColumnSchema("createdat", false, ColumnDataType.DateTime)
            ]),
            var x when x == ImportFileTypeCodes.Products => new FileSchema(ImportFileTypeCodes.Products,
            [
                new ColumnSchema("sku", true, ColumnDataType.String),
                new ColumnSchema("name", true, ColumnDataType.String),
                new ColumnSchema("price", true, ColumnDataType.Decimal, Precision: 18, Scale: 2),
                new ColumnSchema("createdat", false, ColumnDataType.DateTime)
            ]),
            var x when x == ImportFileTypeCodes.FinancialEntry => new FileSchema(ImportFileTypeCodes.FinancialEntry,
            [
                new ColumnSchema("ordernumber", true, ColumnDataType.String),
                new ColumnSchema("customeremail", true, ColumnDataType.Email),
                new ColumnSchema("productsku", true, ColumnDataType.String),
                new ColumnSchema("quantity", true, ColumnDataType.Int),
                new ColumnSchema("orderedat", true, ColumnDataType.DateTime)
            ]),
            var x when x == ImportFileTypeCodes.SalesInvoice => new FileSchema(ImportFileTypeCodes.SalesInvoice,
            [
                new ColumnSchema("documentnumber", true, ColumnDataType.String),
                new ColumnSchema("transactiondate", true, ColumnDataType.DateTime),
                new ColumnSchema("customercode", true, ColumnDataType.String),
                new ColumnSchema("customername", true, ColumnDataType.String),
                new ColumnSchema("productcode", true, ColumnDataType.String),
                new ColumnSchema("productdescription", true, ColumnDataType.String),
                new ColumnSchema("quantity", true, ColumnDataType.Decimal, Precision: 18, Scale: 3),
                new ColumnSchema("unitprice", true, ColumnDataType.Decimal, Precision: 18, Scale: 2),
                new ColumnSchema("totalamount", true, ColumnDataType.Decimal, Precision: 18, Scale: 2),
                new ColumnSchema("transactiontype", false, ColumnDataType.String),
                new ColumnSchema("city", true, ColumnDataType.String),
                new ColumnSchema("productgroup", true, ColumnDataType.String),
                new ColumnSchema("grossweightkg", true, ColumnDataType.Decimal, Precision: 18, Scale: 3)
            ]),
            _ => new FileSchema(string.Empty, [])
        };
    }
}
