namespace InovaSkill.Importer.Domain.Entities;

public sealed class DailyInventoryRecord
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public DateOnly Date { get; set; }
    public decimal ProductionQuantity { get; set; }
    public decimal OutboundQuantity { get; set; }
    public decimal AdjustmentQuantity { get; set; }
    public decimal ClosingQuantity { get; set; }
    public decimal? FirstShiftProductionQuantity { get; set; }
    public decimal? SecondShiftProductionQuantity { get; set; }
    public decimal? ThirdShiftProductionQuantity { get; set; }
    public int SourceRowNumber { get; set; }
    public string SourceSheetName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
