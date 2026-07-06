using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class FiscalMovementsProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    FiscalMovementsSpreadsheetParser parser) : IDataSourceProcessor
{
    private const int DocumentBatchSize = 500;

    public string SourceCode => FiscalImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        var now = DateTime.UtcNow;
        var customerSourceId = await dbContext.DataSources.Where(x => x.Code == CustomerImportCodes.DataSource)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var customers = customerSourceId is null
            ? new Dictionary<string, Guid>()
            : await dbContext.Customers.Where(x => x.DataSourceId == customerSourceId)
                .ToDictionaryAsync(x => $"{x.ExternalCode}|{x.BranchCode}", x => x.Id, cancellationToken);
        var municipalities = (await dbContext.Municipalities.AsNoTracking().ToListAsync(cancellationToken))
            .GroupBy(x => MunicipalityNameNormalizer.Normalize(x.Name))
            .Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single());
        var productCodes = parsed.Rows.Select(x => x.ProductCode).Where(x => x.Length > 0).Distinct().ToArray();
        var products = await dbContext.Products.Where(x => x.DataSourceId == import.DataSourceId &&
            productCodes.Contains(x.ExternalCode)).ToDictionaryAsync(x => x.ExternalCode, cancellationToken);
        foreach (var row in parsed.Rows.DistinctBy(x => x.ProductCode))
        {
            if (string.IsNullOrWhiteSpace(row.ProductCode)) continue;
            if (!products.TryGetValue(row.ProductCode, out var product))
            {
                product = new Product { Id = Guid.NewGuid(), DataSourceId = import.DataSourceId,
                    ExternalCode = row.ProductCode, Description = row.ProductDescription, CreatedAt = now, UpdatedAt = now };
                products.Add(row.ProductCode, product);
                dbContext.Products.Add(product);
            }
            else if (row.ProductDescription.Length > 0)
            {
                product.Description = row.ProductDescription;
                product.UpdatedAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var documentGroups = parsed.Rows.GroupBy(DocumentKey).ToArray();
        foreach (var batch in documentGroups.Chunk(DocumentBatchSize))
        {
            var documentNumbers = batch.Select(group => group.First().DocumentNumber).Distinct().ToArray();
            var existingDocuments = await dbContext.FiscalDocuments.Include(x => x.Items)
                .Where(x => x.DataSourceId == import.DataSourceId && documentNumbers.Contains(x.DocumentNumber))
                .ToListAsync(cancellationToken);
            var documentsByKey = existingDocuments.ToDictionary(DocumentKey);
            foreach (var group in batch)
            {
                var row = group.First();
                documentsByKey.TryGetValue(DocumentKey(row), out var document);
                customers.TryGetValue($"{row.CustomerCode}|{row.BranchCode}", out var customerId);
                municipalities.TryGetValue(MunicipalityNameNormalizer.Normalize(row.CityName), out var municipality);
                if (document is null)
                {
                    document = new FiscalDocument {
                        Id = Guid.NewGuid(), DataSourceId = import.DataSourceId, DocumentNumber = row.DocumentNumber,
                        Series = row.Series, DocumentType = row.DocumentType, MovementType = row.DocumentType,
                        IssueDate = row.IssueDate, CustomerId = customerId == Guid.Empty ? null : customerId,
                        MunicipalityId = municipality?.Id, CustomerCodeAtIssue = row.CustomerCode,
                        BranchCodeAtIssue = row.BranchCode, CustomerNameAtIssue = row.CustomerName,
                        CityNameAtIssue = row.CityName, StateCodeAtIssue = row.StateCode,
                        OperationCode = row.OperationCode, OperationDescription = row.OperationDescription,
                        MovementCategory = FiscalOperationClassifier.Classify(row.OperationCode, row.OperationDescription),
                        OriginalDocumentNumber = row.OriginalDocumentNumber, FirstSeenImportId = importId,
                        LastSeenImportId = importId, CreatedAt = now, UpdatedAt = now };
                    dbContext.FiscalDocuments.Add(document);
                }
                else
                {
                    document.LastSeenImportId = importId;
                    document.CustomerId ??= customerId == Guid.Empty ? null : customerId;
                    document.MunicipalityId ??= municipality?.Id;
                    document.UpdatedAt = now;
                }
                foreach (var itemRow in group.GroupBy(x => x.ItemNumber).Select(x => x.Last()))
                {
                    var item = document.Items.SingleOrDefault(x => x.ItemNumber == itemRow.ItemNumber);
                    item ??= new FiscalDocumentItem { Id = Guid.NewGuid(), FiscalDocument = document,
                        ItemNumber = itemRow.ItemNumber, CreatedAt = now };
                    if (item.FiscalDocumentId == Guid.Empty && !document.Items.Contains(item)) document.Items.Add(item);
                    item.ProductId = products.GetValueOrDefault(itemRow.ProductCode)?.Id;
                    item.ProductCode = itemRow.ProductCode; item.ProductDescription = itemRow.ProductDescription;
                    item.ProductGroupCode = itemRow.ProductGroupCode; item.ProductGroupDescription = itemRow.ProductGroupDescription;
                    item.Quantity = itemRow.Quantity; item.GrossWeightKg = itemRow.GrossWeightKg;
                    item.UnitValue = itemRow.UnitValue; item.SourceTotalValue = itemRow.SourceTotalValue;
                    item.Expenses = itemRow.Expenses; item.Ipi = itemRow.Ipi; item.Icms = itemRow.Icms; item.Iss = itemRow.Iss;
                    item.CfopCode = itemRow.CfopCode; item.CfopDescription = itemRow.CfopDescription;
                    item.TesCode = itemRow.TesCode; item.TesDescription = itemRow.TesDescription;
                    item.OrderNumber = itemRow.OrderNumber; item.WarehouseCode = itemRow.WarehouseCode; item.UpdatedAt = now;
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        await dbContext.RouteImports.Where(x => x.Id == importId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.TotalRows, parsed.TotalRows)
            .SetProperty(x => x.ImportedRows, parsed.Rows.Count)
            .SetProperty(x => x.Status, RouteImportStatus.Completed)
            .SetProperty(x => x.FinishedAt, now)
            .SetProperty(x => x.FailureMessage, (string?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string DocumentKey(ParsedFiscalMovementRow row) =>
        $"{row.DocumentType}|{row.DocumentNumber}|{row.Series}|{row.IssueDate:yyyyMMdd}|{row.CustomerCode}|{row.BranchCode}";

    private static string DocumentKey(FiscalDocument document) =>
        $"{document.DocumentType}|{document.DocumentNumber}|{document.Series}|{document.IssueDate:yyyyMMdd}|{document.CustomerCodeAtIssue}|{document.BranchCodeAtIssue}";
}
