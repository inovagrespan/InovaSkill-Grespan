using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileTypeDetector : IFileTypeDetector
{
    public string? DetectCode(IReadOnlyDictionary<string, string> row)
    {
        var salesKeys = new[]
        {
            "documentnumber", "transactiondate", "customercode", "productcode", "totalamount",
            "documento", "data", "produto", "quantidade", "total"
        };

        var salesScore = salesKeys.Count(row.ContainsKey);
        if (salesScore >= 3 || (row.ContainsKey("documentnumber") && row.ContainsKey("productcode")))
        {
            return ImportFileTypeCodes.SalesInvoice;
        }

        if (HasAny(row, "ordernumber", "quantity", "productsku", "customeremail", "orderedat", "orderdate"))
        {
            return ImportFileTypeCodes.FinancialEntry;
        }

        if (HasAny(row, "sku", "price"))
        {
            return ImportFileTypeCodes.ProductList;
        }

        if (HasAny(row, "email", "name", "createdat", "nome", "cliente"))
        {
            return ImportFileTypeCodes.CustomerList;
        }

        return null;
    }

    private static bool HasAny(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        return keys.Any(k => row.ContainsKey(k));
    }
}
