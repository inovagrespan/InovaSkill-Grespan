using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileTypeDetector : IFileTypeDetector
{
    public string? DetectCode(IReadOnlyDictionary<string, string> row)
    {
        if (HasAny(
            row,
            "documentnumber",
            "transactiondate",
            "customercode",
            "productcode",
            "totalamount",
            "documento",
            "data",
            "cliente",
            "produto",
            "quantidade",
            "total"))
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

        if (HasAny(row, "email", "name", "createdat"))
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
