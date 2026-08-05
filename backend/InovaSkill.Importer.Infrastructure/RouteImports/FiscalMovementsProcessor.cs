using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class FiscalMovementsProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    FiscalMovementsSpreadsheetParser parser,
    IServiceScopeFactory scopeFactory) : IDataSourceProcessor
{
    private const int RowBatchSize = 500;
    private static readonly TimeSpan ProgressPersistenceInterval = TimeSpan.FromSeconds(10);

    public string SourceCode => FiscalImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var now = DateTime.UtcNow;
        var customerSourceId = await dbContext.DataSources.Where(x => x.Code == CustomerImportCodes.DataSource)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var customers = customerSourceId is null
            ? new Dictionary<string, Guid>()
            : await dbContext.Customers.AsNoTracking().Where(x => x.DataSourceId == customerSourceId)
                .ToDictionaryAsync(x => $"{x.ExternalCode}|{x.BranchCode}", x => x.Id, cancellationToken);
        var municipalities = (await dbContext.Municipalities.AsNoTracking().ToListAsync(cancellationToken))
            .GroupBy(x => MunicipalityNameNormalizer.Normalize(x.Name))
            .Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single().Id);

        var detectedTotalRows = 0;
        var importedRows = 0;
        var lastProgressPersistence = DateTime.MinValue;
        var batch = new List<ParsedFiscalMovementRow>(RowBatchSize);
        foreach (var row in parser.StreamRows(content, total => detectedTotalRows = total))
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(row);
            if (batch.Count < RowBatchSize) continue;
            await ProcessBatchAsync(import, batch, customers, municipalities, now, cancellationToken);
            importedRows += batch.Count;
            batch.Clear();
            dbContext.ChangeTracker.Clear();
            if (DateTime.UtcNow - lastProgressPersistence >= ProgressPersistenceInterval)
            {
                await PersistProgressAsync(importId, detectedTotalRows, importedRows, cancellationToken);
                lastProgressPersistence = DateTime.UtcNow;
            }
        }
        if (batch.Count > 0)
        {
            await ProcessBatchAsync(import, batch, customers, municipalities, now, cancellationToken);
            importedRows += batch.Count;
            batch.Clear();
            dbContext.ChangeTracker.Clear();
        }

        await dbContext.RouteImports.Where(x => x.Id == importId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.TotalRows, importedRows)
            .SetProperty(x => x.ImportedRows, importedRows)
            .SetProperty(x => x.Status, RouteImportStatus.Completed)
            .SetProperty(x => x.FinishedAt, DateTime.UtcNow)
            .SetProperty(x => x.FailureMessage, (string?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ProcessBatchAsync(
        RouteImport import,
        IReadOnlyList<ParsedFiscalMovementRow> rows,
        IReadOnlyDictionary<string, Guid> customers,
        IReadOnlyDictionary<string, Guid> municipalities,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var productCodes = rows.Select(x => x.ProductCode).Where(x => x.Length > 0).Distinct().ToArray();
        var matchingProducts = await dbContext.Products
            .Where(x => productCodes.Contains(x.ErpCode) || productCodes.Contains(x.ExternalCode))
            .ToListAsync(cancellationToken);
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in matchingProducts)
        {
            if (!string.IsNullOrWhiteSpace(product.ErpCode)) products.TryAdd(product.ErpCode, product);
            if (!string.IsNullOrWhiteSpace(product.ExternalCode)) products.TryAdd(product.ExternalCode, product);
        }
        foreach (var row in rows.DistinctBy(x => x.ProductCode))
        {
            if (string.IsNullOrWhiteSpace(row.ProductCode)) continue;
            if (!products.TryGetValue(row.ProductCode, out var product))
            {
                product = new Product {
                    Id = Guid.NewGuid(), DataSourceId = import.DataSourceId,
                    ErpCode = row.ProductCode, ExternalCode = row.ProductCode,
                    Name = row.ProductDescription, Description = row.ProductDescription,
                    GroupCode = row.ProductGroupCode, CreatedAt = now, UpdatedAt = now
                };
                products.Add(row.ProductCode, product);
                dbContext.Products.Add(product);
            }
            else if (row.ProductDescription.Length > 0)
            {
                product.Description = row.ProductDescription;
                product.Name = row.ProductDescription;
                if (row.ProductGroupCode.Length > 0) product.GroupCode = row.ProductGroupCode;
                product.UpdatedAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var documentGroups = rows.GroupBy(DocumentKey).ToArray();
        var documentNumbers = documentGroups.Select(group => group.First().DocumentNumber).Distinct().ToArray();
        var existingDocuments = await dbContext.FiscalDocuments.Include(x => x.Items)
            .Where(x => x.DataSourceId == import.DataSourceId && documentNumbers.Contains(x.DocumentNumber))
            .ToListAsync(cancellationToken);
        var documentsByKey = existingDocuments.ToDictionary(DocumentKey);
        foreach (var group in documentGroups)
        {
            var row = group.First();
            documentsByKey.TryGetValue(DocumentKey(row), out var document);
            customers.TryGetValue($"{row.CustomerCode}|{row.BranchCode}", out var customerId);
            municipalities.TryGetValue(MunicipalityNameNormalizer.Normalize(row.CityName), out var municipalityId);
            if (document is null)
            {
                document = new FiscalDocument {
                    Id = Guid.NewGuid(), DataSourceId = import.DataSourceId, DocumentNumber = row.DocumentNumber,
                    Series = row.Series, DocumentType = row.DocumentType, MovementType = row.DocumentType,
                    IssueDate = row.IssueDate, CustomerId = customerId == Guid.Empty ? null : customerId,
                    MunicipalityId = municipalityId == Guid.Empty ? null : municipalityId,
                    CustomerCodeAtIssue = row.CustomerCode, BranchCodeAtIssue = row.BranchCode,
                    CustomerNameAtIssue = row.CustomerName, CityNameAtIssue = row.CityName,
                    StateCodeAtIssue = row.StateCode, OperationCode = row.OperationCode,
                    OperationDescription = row.OperationDescription,
                    MovementCategory = FiscalOperationClassifier.Classify(row.OperationCode, row.OperationDescription),
                    OriginalDocumentNumber = row.OriginalDocumentNumber, FirstSeenImportId = import.Id,
                    LastSeenImportId = import.Id, CreatedAt = now, UpdatedAt = now
                };
                documentsByKey.Add(DocumentKey(row), document);
                dbContext.FiscalDocuments.Add(document);
            }
            else
            {
                document.LastSeenImportId = import.Id;
                document.CustomerId ??= customerId == Guid.Empty ? null : customerId;
                document.MunicipalityId ??= municipalityId == Guid.Empty ? null : municipalityId;
                document.UpdatedAt = now;
            }
            foreach (var itemRow in group.GroupBy(x => x.ItemNumber).Select(x => x.Last()))
            {
                var item = document.Items.SingleOrDefault(x => x.ItemNumber == itemRow.ItemNumber);
                item ??= new FiscalDocumentItem {
                    Id = Guid.NewGuid(), FiscalDocument = document, ItemNumber = itemRow.ItemNumber, CreatedAt = now
                };
                if (item.FiscalDocumentId == Guid.Empty && !document.Items.Contains(item)) document.Items.Add(item);
                item.ProductId = products.GetValueOrDefault(itemRow.ProductCode)?.Id;
                item.ProductCode = itemRow.ProductCode;
                item.ProductDescription = itemRow.ProductDescription;
                item.ProductGroupCode = itemRow.ProductGroupCode;
                item.ProductGroupDescription = itemRow.ProductGroupDescription;
                item.Quantity = itemRow.Quantity;
                item.GrossWeightKg = itemRow.GrossWeightKg;
                item.UnitValue = itemRow.UnitValue;
                item.SourceTotalValue = itemRow.SourceTotalValue;
                item.Expenses = itemRow.Expenses;
                item.Ipi = itemRow.Ipi;
                item.Icms = itemRow.Icms;
                item.Iss = itemRow.Iss;
                item.CfopCode = itemRow.CfopCode;
                item.CfopDescription = itemRow.CfopDescription;
                item.TesCode = itemRow.TesCode;
                item.TesDescription = itemRow.TesDescription;
                item.OrderNumber = itemRow.OrderNumber;
                item.WarehouseCode = itemRow.WarehouseCode;
                item.UpdatedAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistProgressAsync(
        Guid importId,
        int totalRows,
        int importedRows,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var progressDbContext = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
        await progressDbContext.RouteImports
            .Where(x => x.Id == importId && x.Status == RouteImportStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.TotalRows, totalRows)
                .SetProperty(x => x.ImportedRows, importedRows), cancellationToken);
    }

    private static string DocumentKey(ParsedFiscalMovementRow row) =>
        $"{row.DocumentType}|{row.DocumentNumber}|{row.Series}|{row.IssueDate:yyyyMMdd}|{row.CustomerCode}|{row.BranchCode}";

    private static string DocumentKey(FiscalDocument document) =>
        $"{document.DocumentType}|{document.DocumentNumber}|{document.Series}|{document.IssueDate:yyyyMMdd}|{document.CustomerCodeAtIssue}|{document.BranchCodeAtIssue}";
}
