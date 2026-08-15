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
        await using var db = CreateDbContext(connection);
        var (import, job) = await SeedAsync(db, RouteImportStatus.Queued);
        var service = CreateService(db, new ClearingProcessor(db));

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
        await using var db = CreateDbContext(connection);
        var (import, job) = await SeedAsync(db, RouteImportStatus.Completed);
        var processor = new CountingProcessor();
        var service = CreateService(db, processor);

        await service.ProcessAsync(import.Id, job.Id, default);

        var persistedJob = await db.JobExecutions.AsNoTracking().SingleAsync();
        Assert.Equal(0, processor.Calls);
        Assert.Equal(0, persistedJob.Attempts);
        Assert.Equal(JobExecutionStatus.Queued, persistedJob.Status);
    }

    private static ImportDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ImportDbContext>().UseSqlite(connection).Options);

    private static ImportProcessingService CreateService(ImportDbContext db, IDataSourceProcessor processor) =>
        new(db, [processor], new NoOpLifecycle(), new NoOpRouteCustomerAssignmentSynchronizer(),
            new NoOpOperationalJobQueue());

    private static async Task<(RouteImport Import, JobExecution Job)> SeedAsync(
        ImportDbContext db,
        RouteImportStatus status)
    {
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = "TEST", ProcessorKey = "test", Name = "Test", Type = "XLSX",
            ImportMode = DataSourceImportMode.Upsert, NextImportVersion = 2, Active = true,
            CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, FileName = "test.xlsx", FilePath = "test.xlsx",
            Version = 1, Status = status, CreatedAt = now,
            FinishedAt = status == RouteImportStatus.Completed ? now : null
        };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = RouteImportCodes.JobType, RelatedEntityId = import.Id,
            Status = JobExecutionStatus.Queued, CreatedAt = now
        };
        db.AddRange(source, import, job);
        await db.SaveChangesAsync();
        return (import, job);
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
