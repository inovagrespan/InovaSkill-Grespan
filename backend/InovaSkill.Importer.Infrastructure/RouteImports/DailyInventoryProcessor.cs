using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class DailyInventoryProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    DailyInventorySpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => DailyInventoryImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        await dbContext.DailyInventoryRecords.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.RouteImportErrors.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        AddParserErrors(importId, parsed.Errors, now);
        var operationalCodes = parsed.Rows.Select(x => x.OperationalCode).Distinct().ToArray();
        var matchedProducts = await dbContext.Products
            .Where(x => operationalCodes.Contains(x.OperationalCode))
            .ToListAsync(cancellationToken);
        var productGroups = matchedProducts
            .GroupBy(x => x.OperationalCode)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var importedRows = 0;
        var relationshipErrors = 0;
        var duplicateWarnings = 0;
        var conflictErrors = 0;
        foreach (var group in parsed.Rows.GroupBy(x => new { x.OperationalCode, x.Date }))
        {
            if (!productGroups.TryGetValue(group.Key.OperationalCode, out var matches))
            {
                foreach (var row in group.Take(1))
                    AddRelationshipError(importId, row, now, "Produto não encontrado pelo código operacional.");
                relationshipErrors++;
                continue;
            }
            if (matches.Length > 1)
            {
                foreach (var row in group.Take(1))
                    AddRelationshipError(importId, row, now, "Código operacional relaciona mais de um produto.");
                relationshipErrors++;
                continue;
            }
            var distinctValues = group.Select(x => new
            {
                x.ProductionQuantity,
                x.OutboundQuantity,
                x.AdjustmentQuantity,
                x.ClosingQuantity
            }).Distinct().ToArray();
            if (distinctValues.Length > 1)
            {
                var row = group.Last();
                dbContext.RouteImportErrors.Add(new RouteImportError
                {
                    Id = Guid.NewGuid(), ImportId = importId, SheetName = row.SheetName,
                    RowNumber = row.RowNumber, Field = "date", RawValue = row.Date.ToString("yyyy-MM-dd"),
                    Message = "Data duplicada para o produto com valores conflitantes.",
                    Status = ImportErrorStatus.Pending, CreatedAt = now
                });
                conflictErrors++;
                continue;
            }
            if (group.Count() > 1)
            {
                var row = group.Last();
                dbContext.RouteImportErrors.Add(new RouteImportError
                {
                    Id = Guid.NewGuid(), ImportId = importId, SheetName = row.SheetName,
                    RowNumber = row.RowNumber, Field = "date", RawValue = row.Date.ToString("yyyy-MM-dd"),
                    Message = "Data duplicada para o produto com valores idênticos; repetição ignorada.",
                    Status = ImportErrorStatus.Resolved,
                    CorrectedValue = row.Date.ToString("yyyy-MM-dd"), ResolvedAt = now, CreatedAt = now
                });
                duplicateWarnings++;
            }
            var selected = group.Last();
            dbContext.DailyInventoryRecords.Add(new DailyInventoryRecord
            {
                Id = Guid.NewGuid(), ImportId = importId, ProductId = matches.Single().Id,
                Date = selected.Date, ProductionQuantity = selected.ProductionQuantity,
                OutboundQuantity = selected.OutboundQuantity, AdjustmentQuantity = selected.AdjustmentQuantity,
                ClosingQuantity = selected.ClosingQuantity, SourceRowNumber = selected.RowNumber,
                SourceSheetName = selected.SheetName, CreatedAt = now
            });
            importedRows++;
        }
        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = importedRows;
        import.ErrorCount = parsed.Errors.Count + relationshipErrors + duplicateWarnings + conflictErrors;
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void AddRelationshipError(Guid importId, ParsedDailyInventoryRow row, DateTime now, string message)
    {
        dbContext.RouteImportErrors.Add(new RouteImportError
        {
            Id = Guid.NewGuid(), ImportId = importId, SheetName = row.SheetName,
            RowNumber = row.RowNumber, Field = "operational_code", RawValue = row.OperationalCode,
            Message = message, Status = ImportErrorStatus.Pending, CreatedAt = now
        });
    }

    private void AddParserErrors(Guid importId, IEnumerable<ParsedImportError> errors, DateTime now)
    {
        foreach (var error in errors)
        {
            dbContext.RouteImportErrors.Add(new RouteImportError
            {
                Id = Guid.NewGuid(), ImportId = importId, SheetName = error.SheetName,
                RowNumber = error.RowNumber, Field = error.Field, RawValue = error.RawValue,
                Message = error.Message, Status = ImportErrorStatus.Pending, CreatedAt = now
            });
        }
    }
}
