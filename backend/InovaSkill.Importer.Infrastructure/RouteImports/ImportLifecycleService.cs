using System.Data;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class ImportLifecycleService(ImportDbContext dbContext) : IImportLifecycleService
{
    public async Task<RouteImport> CreateAsync(
        Guid dataSourceId,
        string fileName,
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LockDataSourceAsync(dataSourceId, cancellationToken);

        var dataSource = await dbContext.DataSources
            .SingleAsync(source => source.Id == dataSourceId, cancellationToken);
        await dbContext.Entry(dataSource).ReloadAsync(cancellationToken);
        var version = dataSource.NextImportVersion;
        dataSource.NextImportVersion++;

        var routeImport = new RouteImport
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSourceId,
            Version = version,
            FileName = fileName,
            FilePath = filePath,
            Status = RouteImportStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.RouteImports.Add(routeImport);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return routeImport;
    }

    public async Task<bool> TryActivateAsync(Guid importId, CancellationToken cancellationToken)
    {
        var importReference = await dbContext.RouteImports.AsNoTracking()
            .Where(routeImport => routeImport.Id == importId)
            .Select(routeImport => new { routeImport.DataSourceId })
            .SingleAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LockDataSourceAsync(importReference.DataSourceId, cancellationToken);

        var candidate = await dbContext.RouteImports
            .SingleAsync(routeImport => routeImport.Id == importId, cancellationToken);
        var dataSource = await dbContext.DataSources
            .SingleAsync(source => source.Id == candidate.DataSourceId, cancellationToken);
        await dbContext.Entry(dataSource).ReloadAsync(cancellationToken);

        if (candidate.Status != RouteImportStatus.Completed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var lastSuccessfulVersion = dataSource.LastSuccessfulImportId.HasValue
            ? await dbContext.RouteImports
                .Where(routeImport => routeImport.Id == dataSource.LastSuccessfulImportId)
                .Select(routeImport => (long?)routeImport.Version)
                .SingleAsync(cancellationToken)
            : null;
        if (!lastSuccessfulVersion.HasValue || candidate.Version > lastSuccessfulVersion.Value)
        {
            dataSource.LastSuccessfulImportId = candidate.Id;
        }

        var activated = false;
        if (dataSource.ImportMode == DataSourceImportMode.Snapshot)
        {
            var currentVersion = dataSource.CurrentImportId.HasValue
                ? await dbContext.RouteImports
                    .Where(routeImport => routeImport.Id == dataSource.CurrentImportId)
                    .Select(routeImport => (long?)routeImport.Version)
                    .SingleAsync(cancellationToken)
                : null;

            if (!currentVersion.HasValue || candidate.Version > currentVersion.Value)
            {
                dataSource.CurrentImportId = candidate.Id;
                activated = true;
            }
        }

        dataSource.StateUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return activated;
    }

    private async Task LockDataSourceAsync(Guid dataSourceId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        var lockKey = BitConverter.ToInt64(dataSourceId.ToByteArray(), 0);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }
}
