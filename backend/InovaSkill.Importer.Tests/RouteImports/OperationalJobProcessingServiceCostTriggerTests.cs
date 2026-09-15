using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OperationalJobProcessingServiceCostTriggerTests
{
    [Theory]
    [InlineData(OperationalJobCodes.DailyRouteOptimization)]
    [InlineData(OperationalJobCodes.CustomerAddressCoordinateEnrichment)]
    [InlineData(OperationalJobCodes.MunicipalityCoordinateEnrichment)]
    public async Task CompletedDependency_QueuesRouteCostConsolidation(string jobType)
    {
        await using var db = CreateDb();
        var routeImportId = Guid.NewGuid();
        var relatedEntityId = jobType == OperationalJobCodes.DailyRouteOptimization
            ? routeImportId
            : Guid.NewGuid();
        await AddCurrentRouteImport(db, routeImportId);
        var job = AddJob(db, jobType, relatedEntityId, "{}");
        await db.SaveChangesAsync();
        var queue = new RecordingJobQueue();
        var service = new OperationalJobProcessingService(db, [new NoOpProcessor(jobType)], queue);

        await service.ProcessAsync(job.Id, default);

        Assert.Equal(OperationalJobCodes.RouteCostConsolidation, queue.JobType);
        Assert.Equal(routeImportId, queue.RelatedEntityId);
    }

    [Fact]
    public async Task CompletedCostJob_WithRerunRequested_QueuesFollowUp()
    {
        await using var db = CreateDb();
        var routeImportId = Guid.NewGuid();
        var job = AddJob(db, OperationalJobCodes.RouteCostConsolidation, routeImportId,
            "{\"rerunRequested\":true}");
        await db.SaveChangesAsync();
        var queue = new RecordingJobQueue();
        var service = new OperationalJobProcessingService(db,
            [new NoOpProcessor(OperationalJobCodes.RouteCostConsolidation)], queue);

        await service.ProcessAsync(job.Id, default);

        Assert.Equal(OperationalJobCodes.RouteCostConsolidation, queue.JobType);
        Assert.Equal(routeImportId, queue.RelatedEntityId);
    }

    private static JobExecution AddJob(ImportDbContext db, string jobType, Guid relatedEntityId,
        string parametersJson)
    {
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = jobType, Queue = "default", ParametersJson = parametersJson,
            RelatedEntityId = relatedEntityId, Status = JobExecutionStatus.Queued, CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job);
        return job;
    }

    private static async Task AddCurrentRouteImport(ImportDbContext db, Guid importId)
    {
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, Name = "Rotas", Type = "EXCEL",
            ProcessorKey = "routes", ImportMode = DataSourceImportMode.Snapshot, Active = true,
            CurrentImportId = importId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.AddRange(source, new RouteImport
        {
            Id = importId, DataSourceId = source.Id, Version = 1, FileName = "rotas.xlsx",
            FilePath = "rotas.xlsx", Status = RouteImportStatus.Completed, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"operational-cost-trigger-{Guid.NewGuid()}").Options);

    private sealed class NoOpProcessor(string jobType) : IOperationalJobProcessor
    {
        public string JobType { get; } = jobType;
        public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingJobQueue : IOperationalJobQueue
    {
        public string? JobType { get; private set; }
        public Guid? RelatedEntityId { get; private set; }

        public Task<Guid?> TryQueueAsync(string jobType, Guid relatedEntityId,
            CancellationToken cancellationToken)
        {
            JobType = jobType;
            RelatedEntityId = relatedEntityId;
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
