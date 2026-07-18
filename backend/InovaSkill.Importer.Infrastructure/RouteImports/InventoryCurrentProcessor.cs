using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class InventoryCurrentProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    InventoryCurrentSpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => InventoryCurrentImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        await dbContext.InventorySnapshots.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.RouteImportErrors.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        AddParserErrors(importId, parsed.Errors, now);
        var erpCodes = parsed.Rows.Select(x => x.ErpCode).Distinct().ToArray();
        var products = await dbContext.Products.Where(x => erpCodes.Contains(x.ErpCode) ||
                erpCodes.Contains(x.ExternalCode))
            .ToDictionaryAsync(x => string.IsNullOrWhiteSpace(x.ErpCode) ? x.ExternalCode : x.ErpCode, cancellationToken);
        var importedRows = 0;
        foreach (var row in parsed.Rows.DistinctBy(x => new { x.ErpCode, x.BranchCode, x.WarehouseCode }))
        {
            if (!products.TryGetValue(row.ErpCode, out var product))
            {
                dbContext.RouteImportErrors.Add(new RouteImportError
                {
                    Id = Guid.NewGuid(), ImportId = importId, SheetName = "Estoque",
                    RowNumber = row.RowNumber, Field = "erp_code", RawValue = row.ErpCode,
                    Message = "Produto não encontrado pelo código ERP. Importe o cadastro de produtos primeiro.",
                    Status = ImportErrorStatus.Pending, CreatedAt = now
                });
                continue;
            }
            if (row.OnHandQuantity - row.CommittedQuantity != row.AvailableQuantity)
            {
                dbContext.RouteImportErrors.Add(new RouteImportError
                {
                    Id = Guid.NewGuid(), ImportId = importId, SheetName = "Estoque",
                    RowNumber = row.RowNumber, Field = "available_quantity",
                    RawValue = row.AvailableQuantity.ToString(),
                    Message = "Disponível difere de saldo físico menos empenhado; valor da planilha foi preservado.",
                    Status = ImportErrorStatus.Resolved,
                    CorrectedValue = row.AvailableQuantity.ToString(),
                    ResolvedAt = now, CreatedAt = now
                });
            }
            dbContext.InventorySnapshots.Add(new InventorySnapshot
            {
                Id = Guid.NewGuid(), ImportId = importId, ProductId = product.Id,
                BranchCode = row.BranchCode, WarehouseCode = row.WarehouseCode,
                OnHandQuantity = row.OnHandQuantity, CommittedQuantity = row.CommittedQuantity,
                AvailableQuantity = row.AvailableQuantity, StockValue = row.StockValue,
                CommittedValue = row.CommittedValue, SourceRowNumber = row.RowNumber,
                CreatedAt = now
            });
            importedRows++;
        }
        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = importedRows;
        import.ErrorCount = parsed.Errors.Count + parsed.Rows.Count(row => !products.ContainsKey(row.ErpCode)) +
            parsed.Rows.Count(row => row.OnHandQuantity - row.CommittedQuantity != row.AvailableQuantity);
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void AddParserErrors(Guid importId, IEnumerable<ParsedImportError> errors, DateTime now)
    {
        foreach (var error in errors)
        {
            dbContext.RouteImportErrors.Add(new RouteImportError
            {
                Id = Guid.NewGuid(), ImportId = importId, SheetName = error.SheetName,
                RowNumber = error.RowNumber, Field = error.Field, RawValue = error.RawValue,
                Message = error.Message, Status = ImportErrorStatus.Resolved,
                CorrectedValue = error.RawValue, ResolvedAt = now, CreatedAt = now
            });
        }
    }
}
