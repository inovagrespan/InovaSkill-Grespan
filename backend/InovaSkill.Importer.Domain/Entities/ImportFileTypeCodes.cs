namespace InovaSkill.Importer.Domain.Entities;

public static class ImportFileTypeCodes
{
    public const string SalesInvoice = "SALES_INVOICE";
    public const string Customers = "CUSTOMER_LIST";
    public const string Products = "PRODUCT_LIST";
    public const string FinancialEntry = "FINANCIAL_ENTRY";
    public const string RoutePlanning = "ROUTE_PLANNING";

    [Obsolete("Use Customers instead. This alias remains only for backward compatibility during the rename.")]
    public const string CustomerList = Customers;

    [Obsolete("Use Products instead. This alias remains only for backward compatibility during the rename.")]
    public const string ProductList = Products;
}

