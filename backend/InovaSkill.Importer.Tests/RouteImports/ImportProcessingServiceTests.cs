using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class ImportProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_ProcessorClearsChangeTracker_StillCompletesPersistedJob()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new DataSource {
            Id = Guid.NewGuid(), Code = "TEST", ProcessorKey = "test", Name = "Test", Type = "XLSX",
            ImportMode = DataSourceImportMode.Upsert, NextImportVersion = 2, Active = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id, FileName = "test.xlsx", FilePath = "test.xlsx",
            Version = 1, Status = RouteImportStatus.Queued, CreatedAt = DateTime.UtcNow
        };
        var job = new JobExecution {
            Id = Guid.NewGuid(), JobType = RouteImportCodes.JobType, RelatedEntityId = import.Id,
            Status = JobExecutionStatus.Queued, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(source, import, job);
        await db.SaveChangesAsync();
        var service = new ImportProcessingService(
            db,
            [new ClearingProcessor(db)],
            new NoOpLifecycle(),
            new NoOpRouteCustomerAssignmentSynchronizer(),
            new NoOpOperationalJobQueue());

        await service.ProcessAsync(import.Id, job.Id, default);

        var persistedJob = await db.JobExecutions.AsNoTracking().SingleAsync();
        Assert.Equal(JobExecutionStatus.Completed, persistedJob.Status);
        Assert.NotNull(persistedJob.FinishedAt);
    }

    [Fact]
    public async Task ProcessAsync_CompletedImport_DoesNotRunProcessorAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new DataSource {
            Id = Guid.NewGuid(), Code = "TEST", ProcessorKey = "test", Name = "Test", Type = "XLSX",
            ImportMode = DataSourceImportMode.Upsert, NextImportVersion = 2, Active = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id, FileName = "test.xlsx", FilePath = "test.xlsx",
            Version = 1, Status = RouteImportStatus.Completed, CreatedAt = DateTime.UtcNow,
            FinishedAt = DateTime.UtcNow
        };
        var job = new JobExecution {
            Id = Guid.NewGuid(), JobType = RouteImportCodes.JobType, RelatedEntityId = import.Id,
            Status = JobExecutionStatus.Queued, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(source, import, job);
        await db.SaveChangesAsync();
        var processor = new CountingProcessor();
        var service = new ImportProcessingService(
            db,
            [processor],
            new NoOpLifecycle(),
            new NoOpRouteCustomerAssignmentSynchronizer(),
            new NoOpOperationalJobQueue());

        await service.ProcessAsync(import.Id, job.Id, default);

        var persistedJob = await db.JobExecutions.AsNoTracking().SingleAsync();
        Assert.Equal(0, processor.Calls);
        Assert.Equal(0, persistedJob.Attempts);
        Assert.Equal(JobExecutionStatus.Queued, persistedJob.Status);
    }

    private sealed class ClearingProcessor(ImportDbContext db) : IDataSourceProcessor
    {
        public string SourceCode => "test";

        public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
        {
            db.ChangeTracker.Clear();
            await db.RouteImports.Where(x => x.Id == importId).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RouteImportStatus.Completed)
                .SetProperty(x => x.FinishedAt, DateTime.UtcNow), cancellationToken);
        }
    }

    private sealed class CountingProcessor : IDataSourceProcessor
    {
        public string SourceCode => "test";

        public int Calls { get; private set; }

        public Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpLifecycle : IImportLifecycleService
    {
        public Task<RouteImport> CreateAsync(Guid dataSourceId, string fileName, string filePath,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> TryActivateAsync(Guid importId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class NoOpOperationalJobQueue : IOperationalJobQueue
    {
        public Task<Guid?> TryQueueAsync(string jobType, Guid relatedEntityId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class NoOpRouteCustomerAssignmentSynchronizer : IRouteCustomerAssignmentSynchronizer
    {
        public Task SyncInferredAssignmentsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
