namespace InovaSkill.Importer.Domain.Entities;

public sealed class InventorySnapshot
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal CommittedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal StockValue { get; set; }
    public decimal CommittedValue { get; set; }
    public int SourceRowNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
