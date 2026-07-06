using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class CustomersProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    CustomersSpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => CustomerImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
        {
            var lockKey = BitConverter.ToInt64(importId.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        }
        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        var rows = parsed.Rows
            .GroupBy(row => new { row.BranchCode, row.ExternalCode })
            .Select(group => group.Last())
            .ToArray();

        await dbContext.CustomerSnapshots.Where(x => x.ImportId == importId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.RouteImportErrors
            .Where(x => x.ImportId == importId && x.Field == "document_number")
            .ExecuteDeleteAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await EnsureMunicipalitiesAsync(rows, now, cancellationToken);
        await EnsureCustomersAsync(import.DataSourceId, rows, now, cancellationToken);

        var municipalities = await dbContext.Municipalities
            .Where(item => rows.Select(row => row.StateCode).Contains(item.StateCode))
            .ToDictionaryAsync(item => $"{item.StateCode}|{item.NormalizedName}", cancellationToken);
        var customers = await dbContext.Customers
            .Where(item => item.DataSourceId == import.DataSourceId)
            .ToDictionaryAsync(item => $"{item.BranchCode}|{item.ExternalCode}", cancellationToken);
        foreach (var row in rows)
        {
            var digits = new string(row.DocumentNumber.Where(char.IsDigit).ToArray());
            var hasDocumentWarning = digits.Length is not (11 or 14) ||
                (digits.Length > 0 && digits.All(character => character == '0'));
            if (hasDocumentWarning)
            {
                dbContext.RouteImportErrors.Add(new RouteImportError
                {
                    Id = Guid.NewGuid(), ImportId = importId, SheetName = "Clientes",
                    RowNumber = row.RowNumber, Field = "document_number", RawValue = row.DocumentNumber,
                    Message = "Documento preservado, mas não possui um formato válido de CPF/CNPJ.",
                    Status = ImportErrorStatus.Resolved, CorrectedValue = row.DocumentNumber,
                    ResolvedAt = now, CreatedAt = now
                });
            }
            dbContext.CustomerSnapshots.Add(new CustomerSnapshot
            {
                Id = Guid.NewGuid(),
                ImportId = importId,
                CustomerId = customers[$"{row.BranchCode}|{row.ExternalCode}"].Id,
                DocumentNumber = row.DocumentNumber,
                DocumentType = digits.Length switch { 11 => "CPF", 14 => "CNPJ", _ => "UNKNOWN" },
                LegalName = row.LegalName,
                TradeName = row.TradeName,
                CustomerType = row.CustomerType,
                MunicipalityId = municipalities[$"{row.StateCode}|{row.NormalizedMunicipalityName}"].Id,
                SourceRowNumber = row.RowNumber,
                CreatedAt = now
            });
        }
        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = rows.Length;
        import.ErrorCount = rows.Count(row =>
        {
            var digits = new string(row.DocumentNumber.Where(char.IsDigit).ToArray());
            return digits.Length is not (11 or 14) ||
                (digits.Length > 0 && digits.All(character => character == '0'));
        });
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureMunicipalitiesAsync(
        IEnumerable<ParsedCustomerRow> rows, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var row in rows.DistinctBy(item => new { item.StateCode, item.NormalizedMunicipalityName }))
        {
            if (dbContext.Database.IsNpgsql())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO municipalities ("Id", "StateCode", "Name", "NormalizedName", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {row.StateCode}, {row.MunicipalityName}, {row.NormalizedMunicipalityName}, {now})
                    ON CONFLICT ("StateCode", "NormalizedName") DO NOTHING
                    """, cancellationToken);
            }
            else if (!await dbContext.Municipalities.AnyAsync(
                item => item.StateCode == row.StateCode && item.NormalizedName == row.NormalizedMunicipalityName,
                cancellationToken))
            {
                dbContext.Municipalities.Add(new Municipality
                {
                    Id = Guid.NewGuid(), StateCode = row.StateCode, Name = row.MunicipalityName,
                    NormalizedName = row.NormalizedMunicipalityName, CreatedAt = now
                });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCustomersAsync(
        Guid dataSourceId, IEnumerable<ParsedCustomerRow> rows, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var row in rows.DistinctBy(item => new { item.BranchCode, item.ExternalCode }))
        {
            if (dbContext.Database.IsNpgsql())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO customers ("Id", "DataSourceId", "BranchCode", "ExternalCode", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {dataSourceId}, {row.BranchCode}, {row.ExternalCode}, {now})
                    ON CONFLICT ("DataSourceId", "BranchCode", "ExternalCode") DO NOTHING
                    """, cancellationToken);
            }
            else if (!await dbContext.Customers.AnyAsync(
                item => item.DataSourceId == dataSourceId && item.BranchCode == row.BranchCode &&
                        item.ExternalCode == row.ExternalCode, cancellationToken))
            {
                dbContext.Customers.Add(new Customer
                {
                    Id = Guid.NewGuid(), DataSourceId = dataSourceId, BranchCode = row.BranchCode,
                    ExternalCode = row.ExternalCode, CreatedAt = now
                });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
