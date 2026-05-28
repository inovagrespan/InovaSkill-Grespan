using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileTypeDetector : IFileTypeDetector
{
    public FileType Detect(IReadOnlyDictionary<string, string> row)
    {
        // Most specific first, to avoid false positives (e.g. Products has "name").
        if (HasAny(row, "ordernumber", "quantity", "productsku", "customeremail", "orderedat", "orderdate"))
        {
            return FileType.Orders;
        }

        if (HasAny(row, "sku", "price"))
        {
            return FileType.Products;
        }

        if (HasAny(row, "email", "name", "createdat"))
        {
            return FileType.Customers;
        }

        return FileType.Unknown;
    }

    private static bool HasAny(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        return keys.Any(k => row.ContainsKey(k));
    }
}
