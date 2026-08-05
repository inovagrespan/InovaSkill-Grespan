using System.Data;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class FiscalMovementsProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    FiscalMovementsSpreadsheetParser parser) : IDataSourceProcessor
{
    private const int StagingBatchSize = 500;
    private const int MergeCommandTimeoutSeconds = 600;
    private static readonly JsonSerializerOptions StagingJsonOptions = new(JsonSerializerDefaults.Web);

    public string SourceCode => FiscalImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        var import = await dbContext.RouteImports.AsNoTracking()
            .SingleAsync(item => item.Id == importId, cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var lockKey = ResolveDataSourceLockKey(import.DataSourceId);
        await SetAdvisoryLockAsync(connection, lockKey, acquire: true, cancellationToken);
        try
        {
            var stagedThroughRow = await GetLastStagedRowAsync(connection, importId, cancellationToken);
            var customerSourceId = await dbContext.DataSources.AsNoTracking()
                .Where(item => item.Code == CustomerImportCodes.DataSource)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
            var customers = customerSourceId is null
                ? new Dictionary<string, Guid>()
                : await dbContext.Customers.AsNoTracking().Where(item => item.DataSourceId == customerSourceId)
                    .ToDictionaryAsync(item => $"{item.ExternalCode}|{item.BranchCode}", item => item.Id, cancellationToken);
            var municipalities = (await dbContext.Municipalities.AsNoTracking().ToListAsync(cancellationToken))
                .GroupBy(item => MunicipalityNameNormalizer.Normalize(item.Name))
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single().Id);
            await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
            var detectedTotalRows = 0;
            var stagedRows = await CountStagedRowsAsync(connection, importId, cancellationToken);
            var batch = new List<FiscalStagingRow>(StagingBatchSize);

            foreach (var row in parser.StreamRows(content, total => detectedTotalRows = total))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row.RowNumber <= stagedThroughRow) continue;
                customers.TryGetValue($"{row.CustomerCode}|{row.BranchCode}", out var customerId);
                municipalities.TryGetValue(MunicipalityNameNormalizer.Normalize(row.CityName), out var municipalityId);
                batch.Add(new FiscalStagingRow(
                    row,
                    customerId == Guid.Empty ? null : customerId,
                    municipalityId == Guid.Empty ? null : municipalityId,
                    FiscalOperationClassifier.Classify(row.OperationCode, row.OperationDescription).ToString()));
                if (batch.Count < StagingBatchSize) continue;
                await WriteStagingBatchAsync(connection, importId, batch, cancellationToken);
                stagedRows += batch.Count;
                batch.Clear();
                await PersistProgressAsync(importId, detectedTotalRows, stagedRows, cancellationToken);
            }
            if (batch.Count > 0)
            {
                await WriteStagingBatchAsync(connection, importId, batch, cancellationToken);
                stagedRows += batch.Count;
                batch.Clear();
                await PersistProgressAsync(importId, detectedTotalRows, stagedRows, cancellationToken);
            }

            await MergeStagingAsync(import.Id, import.DataSourceId, stagedRows, cancellationToken);
        }
        finally
        {
            await SetAdvisoryLockAsync(connection, lockKey, acquire: false, CancellationToken.None);
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task WriteStagingBatchAsync(
        NpgsqlConnection connection,
        Guid importId,
        IReadOnlyList<FiscalStagingRow> rows,
        CancellationToken cancellationToken)
    {
        await using var importer = await connection.BeginBinaryImportAsync("""
            COPY fiscal_import_staging
                ("ImportId", "RowNumber", "CustomerId", "MunicipalityId", "MovementCategory", "Payload", "CreatedAt")
            FROM STDIN (FORMAT BINARY)
            """, cancellationToken);
        foreach (var staged in rows)
        {
            await importer.StartRowAsync(cancellationToken);
            await importer.WriteAsync(importId, NpgsqlDbType.Uuid, cancellationToken);
            await importer.WriteAsync(staged.Row.RowNumber, NpgsqlDbType.Integer, cancellationToken);
            if (staged.CustomerId.HasValue)
                await importer.WriteAsync(staged.CustomerId.Value, NpgsqlDbType.Uuid, cancellationToken);
            else await importer.WriteNullAsync(cancellationToken);
            if (staged.MunicipalityId.HasValue)
                await importer.WriteAsync(staged.MunicipalityId.Value, NpgsqlDbType.Uuid, cancellationToken);
            else await importer.WriteNullAsync(cancellationToken);
            await importer.WriteAsync(staged.MovementCategory, NpgsqlDbType.Varchar, cancellationToken);
            await importer.WriteAsync(JsonSerializer.Serialize(staged.Row, StagingJsonOptions), NpgsqlDbType.Jsonb, cancellationToken);
            await importer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz, cancellationToken);
        }
        await importer.CompleteAsync(cancellationToken);
    }

    private async Task MergeStagingAsync(
        Guid importId,
        Guid dataSourceId,
        int stagedRows,
        CancellationToken cancellationToken)
    {
        dbContext.Database.SetCommandTimeout(MergeCommandTimeoutSeconds);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var now = DateTime.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            WITH staged AS (
                SELECT DISTINCT ON ("Payload"->>'productCode')
                    "Payload"->>'productCode' AS code,
                    "Payload"->>'productDescription' AS description,
                    "Payload"->>'productGroupCode' AS group_code
                FROM fiscal_import_staging
                WHERE "ImportId" = {{importId}} AND COALESCE("Payload"->>'productCode', '') <> ''
                ORDER BY "Payload"->>'productCode', "RowNumber" DESC
            )
            UPDATE products AS product
            SET "Name" = staged.description,
                "Description" = staged.description,
                "GroupCode" = CASE WHEN staged.group_code = '' THEN product."GroupCode" ELSE staged.group_code END,
                "UpdatedAt" = {{now}}
            FROM staged
            WHERE product."ErpCode" = staged.code OR product."ExternalCode" = staged.code;

            WITH staged AS (
                SELECT DISTINCT ON ("Payload"->>'productCode')
                    "Payload"->>'productCode' AS code,
                    "Payload"->>'productDescription' AS description,
                    "Payload"->>'productGroupCode' AS group_code
                FROM fiscal_import_staging
                WHERE "ImportId" = {{importId}} AND COALESCE("Payload"->>'productCode', '') <> ''
                ORDER BY "Payload"->>'productCode', "RowNumber" DESC
            )
            INSERT INTO products
                ("Id", "DataSourceId", "ExternalCode", "Description", "ErpCode", "OperationalCode", "Name",
                 "Type", "Unit", "GroupCode", "Gtin", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), {{dataSourceId}}, staged.code, staged.description, staged.code, '', staged.description,
                   '', '', staged.group_code, '', {{now}}, {{now}}
            FROM staged
            WHERE NOT EXISTS (SELECT 1 FROM products product
                              WHERE product."ErpCode" = staged.code OR product."ExternalCode" = staged.code);
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            WITH staged AS (
                SELECT DISTINCT ON (
                    "Payload"->>'documentType', "Payload"->>'documentNumber', "Payload"->>'series',
                    ("Payload"->>'issueDate')::date, "Payload"->>'customerCode', "Payload"->>'branchCode')
                    "CustomerId" AS customer_id, "MunicipalityId" AS municipality_id,
                    "MovementCategory" AS movement_category, "Payload" AS payload
                FROM fiscal_import_staging
                WHERE "ImportId" = {{importId}}
                ORDER BY "Payload"->>'documentType', "Payload"->>'documentNumber', "Payload"->>'series',
                    ("Payload"->>'issueDate')::date, "Payload"->>'customerCode', "Payload"->>'branchCode', "RowNumber" DESC
            )
            INSERT INTO fiscal_documents
                ("Id", "DataSourceId", "DocumentNumber", "Series", "DocumentType", "MovementType", "IssueDate",
                 "CustomerId", "MunicipalityId", "CustomerCodeAtIssue", "BranchCodeAtIssue", "CustomerNameAtIssue",
                 "CityNameAtIssue", "StateCodeAtIssue", "OperationCode", "OperationDescription", "MovementCategory",
                 "OriginalDocumentNumber", "FirstSeenImportId", "LastSeenImportId", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), {{dataSourceId}}, payload->>'documentNumber', payload->>'series',
                   payload->>'documentType', payload->>'documentType', (payload->>'issueDate')::date,
                   customer_id, municipality_id, payload->>'customerCode', payload->>'branchCode',
                   payload->>'customerName', payload->>'cityName', payload->>'stateCode', payload->>'operationCode',
                   payload->>'operationDescription', movement_category, NULLIF(payload->>'originalDocumentNumber', ''),
                   {{importId}}, {{importId}}, {{now}}, {{now}}
            FROM staged
            ON CONFLICT ("DataSourceId", "DocumentType", "DocumentNumber", "Series", "IssueDate",
                         "CustomerCodeAtIssue", "BranchCodeAtIssue")
            DO UPDATE SET
                "CustomerId" = COALESCE(fiscal_documents."CustomerId", EXCLUDED."CustomerId"),
                "MunicipalityId" = COALESCE(fiscal_documents."MunicipalityId", EXCLUDED."MunicipalityId"),
                "CustomerNameAtIssue" = EXCLUDED."CustomerNameAtIssue",
                "CityNameAtIssue" = EXCLUDED."CityNameAtIssue",
                "StateCodeAtIssue" = EXCLUDED."StateCodeAtIssue",
                "OperationCode" = EXCLUDED."OperationCode",
                "OperationDescription" = EXCLUDED."OperationDescription",
                "MovementCategory" = EXCLUDED."MovementCategory",
                "OriginalDocumentNumber" = EXCLUDED."OriginalDocumentNumber",
                "LastSeenImportId" = EXCLUDED."LastSeenImportId",
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            WITH parsed AS MATERIALIZED (
                SELECT "RowNumber", "Payload" AS payload,
                    "Payload"->>'documentType' AS document_type,
                    "Payload"->>'documentNumber' AS document_number,
                    "Payload"->>'series' AS series,
                    ("Payload"->>'issueDate')::date AS issue_date,
                    "Payload"->>'customerCode' AS customer_code,
                    "Payload"->>'branchCode' AS branch_code,
                    "Payload"->>'itemNumber' AS item_number,
                    "Payload"->>'productCode' AS product_code
                FROM fiscal_import_staging
                WHERE "ImportId" = {{importId}}
            ), staged AS (
                SELECT DISTINCT ON (document_type, document_number, series, issue_date,
                                    customer_code, branch_code, item_number) *
                FROM parsed
                ORDER BY document_type, document_number, series, issue_date,
                         customer_code, branch_code, item_number, "RowNumber" DESC
            ), resolved AS (
                SELECT document."Id" AS document_id,
                       COALESCE(product."Id", external_product."Id") AS product_id,
                       staged.payload
                FROM staged
                JOIN fiscal_documents document ON document."DataSourceId" = {{dataSourceId}}
                 AND document."DocumentType" = staged.document_type
                 AND document."DocumentNumber" = staged.document_number
                 AND document."Series" = staged.series
                 AND document."IssueDate" = staged.issue_date
                 AND document."CustomerCodeAtIssue" = staged.customer_code
                 AND document."BranchCodeAtIssue" = staged.branch_code
                LEFT JOIN products product ON product."ErpCode" = staged.product_code
                LEFT JOIN LATERAL (
                    SELECT fallback."Id" FROM products fallback
                    WHERE product."Id" IS NULL AND fallback."ExternalCode" = staged.product_code
                    LIMIT 1
                ) external_product ON true
            )
            INSERT INTO fiscal_document_items
                ("Id", "FiscalDocumentId", "ItemNumber", "ProductId", "ProductCode", "ProductDescription",
                 "ProductGroupCode", "ProductGroupDescription", "Quantity", "GrossWeightKg", "UnitValue",
                 "SourceTotalValue", "Expenses", "Ipi", "Icms", "Iss", "CfopCode", "CfopDescription",
                 "TesCode", "TesDescription", "OrderNumber", "WarehouseCode", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), document_id, payload->>'itemNumber', product_id, payload->>'productCode',
                   payload->>'productDescription', payload->>'productGroupCode', payload->>'productGroupDescription',
                   (payload->>'quantity')::numeric, (payload->>'grossWeightKg')::numeric,
                   NULLIF(payload->>'unitValue', '')::numeric, NULLIF(payload->>'sourceTotalValue', '')::numeric,
                   NULLIF(payload->>'expenses', '')::numeric, NULLIF(payload->>'ipi', '')::numeric,
                   NULLIF(payload->>'icms', '')::numeric, NULLIF(payload->>'iss', '')::numeric,
                   NULLIF(payload->>'cfopCode', ''), NULLIF(payload->>'cfopDescription', ''),
                   NULLIF(payload->>'tesCode', ''), NULLIF(payload->>'tesDescription', ''),
                   NULLIF(payload->>'orderNumber', ''), NULLIF(payload->>'warehouseCode', ''), {{now}}, {{now}}
            FROM resolved
            ON CONFLICT ("FiscalDocumentId", "ItemNumber") DO UPDATE SET
                "ProductId" = EXCLUDED."ProductId", "ProductCode" = EXCLUDED."ProductCode",
                "ProductDescription" = EXCLUDED."ProductDescription", "ProductGroupCode" = EXCLUDED."ProductGroupCode",
                "ProductGroupDescription" = EXCLUDED."ProductGroupDescription", "Quantity" = EXCLUDED."Quantity",
                "GrossWeightKg" = EXCLUDED."GrossWeightKg", "UnitValue" = EXCLUDED."UnitValue",
                "SourceTotalValue" = EXCLUDED."SourceTotalValue", "Expenses" = EXCLUDED."Expenses",
                "Ipi" = EXCLUDED."Ipi", "Icms" = EXCLUDED."Icms", "Iss" = EXCLUDED."Iss",
                "CfopCode" = EXCLUDED."CfopCode", "CfopDescription" = EXCLUDED."CfopDescription",
                "TesCode" = EXCLUDED."TesCode", "TesDescription" = EXCLUDED."TesDescription",
                "OrderNumber" = EXCLUDED."OrderNumber", "WarehouseCode" = EXCLUDED."WarehouseCode",
                "UpdatedAt" = EXCLUDED."UpdatedAt";

            DELETE FROM fiscal_import_staging WHERE "ImportId" = {{importId}};
            UPDATE imports SET "TotalRows" = {{stagedRows}}, "ImportedRows" = {{stagedRows}},
                "Status" = 'Completed', "FinishedAt" = {{DateTime.UtcNow}}, "FailureMessage" = NULL
            WHERE "Id" = {{importId}};
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task PersistProgressAsync(Guid importId, int totalRows, int importedRows, CancellationToken cancellationToken) =>
        await dbContext.RouteImports.Where(item => item.Id == importId && item.Status == RouteImportStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.TotalRows, totalRows)
                .SetProperty(item => item.ImportedRows, importedRows), cancellationToken);

    private static async Task<int> GetLastStagedRowAsync(NpgsqlConnection connection, Guid importId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(MAX(\"RowNumber\"), 0) FROM fiscal_import_staging WHERE \"ImportId\" = @importId", connection);
        command.Parameters.AddWithValue("importId", importId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> CountStagedRowsAsync(NpgsqlConnection connection, Guid importId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM fiscal_import_staging WHERE \"ImportId\" = @importId", connection);
        command.Parameters.AddWithValue("importId", importId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task SetAdvisoryLockAsync(
        NpgsqlConnection connection, long key, bool acquire, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            acquire ? "SELECT pg_advisory_lock(@key)" : "SELECT pg_advisory_unlock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static long ResolveDataSourceLockKey(Guid dataSourceId) =>
        BitConverter.ToInt64(dataSourceId.ToByteArray(), 0);

    private sealed record FiscalStagingRow(
        ParsedFiscalMovementRow Row,
        Guid? CustomerId,
        Guid? MunicipalityId,
        string MovementCategory);
}
