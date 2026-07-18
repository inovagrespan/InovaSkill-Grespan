using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class ProductsProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    ProductsSpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => ProductImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        await dbContext.RouteImportErrors.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var error in parsed.Errors)
        {
            dbContext.RouteImportErrors.Add(new RouteImportError
            {
                Id = Guid.NewGuid(), ImportId = importId, SheetName = error.SheetName,
                RowNumber = error.RowNumber, Field = error.Field, RawValue = error.RawValue,
                Message = error.Message, Status = ImportErrorStatus.Resolved,
                CorrectedValue = error.RawValue, ResolvedAt = now, CreatedAt = now
            });
        }
        var validRows = parsed.Rows
            .GroupBy(x => x.ErpCode)
            .Select(group =>
            {
                var distinct = group.Select(x => new { x.OperationalCode, x.Name }).Distinct().ToArray();
                if (distinct.Length > 1)
                    dbContext.RouteImportErrors.Add(new RouteImportError
                    {
                        Id = Guid.NewGuid(), ImportId = importId, SheetName = "Produtos",
                        RowNumber = group.Last().RowNumber, Field = "erp_code", RawValue = group.Key,
                        Message = "Código ERP duplicado com dados conflitantes; a última linha foi preservada.",
                        Status = ImportErrorStatus.Resolved, CorrectedValue = group.Key,
                        ResolvedAt = now, CreatedAt = now
                    });
                return group.Last();
            })
            .ToArray();
        var erpCodes = validRows.Select(x => x.ErpCode).Distinct().ToArray();
        var products = await dbContext.Products.Where(x => erpCodes.Contains(x.ErpCode) ||
                erpCodes.Contains(x.ExternalCode))
            .ToDictionaryAsync(x => string.IsNullOrWhiteSpace(x.ErpCode) ? x.ExternalCode : x.ErpCode, cancellationToken);

        foreach (var row in validRows)
        {
            if (!products.TryGetValue(row.ErpCode, out var product))
            {
                product = new Product { Id = Guid.NewGuid(), CreatedAt = now };
                dbContext.Products.Add(product);
                products.Add(row.ErpCode, product);
            }
            product.ErpCode = row.ErpCode;
            product.ExternalCode = row.ErpCode;
            product.OperationalCode = row.OperationalCode;
            product.Name = row.Name;
            product.Description = row.Name;
            product.Type = row.Type;
            product.Unit = row.Unit;
            product.GroupCode = row.GroupCode;
            product.NetWeightKg = row.NetWeightKg;
            product.GrossWeightKg = row.GrossWeightKg;
            product.Gtin = row.Gtin;
            product.UpdatedAt = now;
        }

        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = validRows.Length;
        import.ErrorCount = parsed.Errors.Count;
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

}
