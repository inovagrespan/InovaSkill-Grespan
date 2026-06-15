using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class JobServiceTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldPersistQueuedJobAndPublishJobRequestedEvent()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob { Id = 10, FilePath = "clientes.csv", OriginalFileName = "clientes.csv" });
        await db.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        var service = new JobService(
            db,
            [new SpreadsheetImportJobPayloadValidator(db)],
            [new NoOpJobHandler(JobTypeCodes.SpreadsheetImport)],
            publisher);

        var jobId = await service.EnqueueAsync(
            JobTypeCodes.SpreadsheetImport,
            new SpreadsheetImportJobPayload(10, "clientes.csv", ImportFileTypeCodes.Customers),
            userId: "user-1",
            CancellationToken.None);

        var job = await db.Jobs.SingleAsync(x => x.Id == jobId);
        Assert.Equal(JobTypeCodes.SpreadsheetImport, job.Type);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("user-1", job.UserId);
        Assert.Contains("\"fileJobId\":10", job.PayloadJson);
        var published = Assert.Single(publisher.Published);
        Assert.Equal(ProcessingEventTypes.JobRequested, published.EventType);
        Assert.Equal(job.Id, published.JobId);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldRejectInvalidPayloadBeforePublishing()
    {
        await using var db = CreateDb();
        var publisher = new RecordingPublisher();
        var service = new JobService(
            db,
            [new SpreadsheetImportJobPayloadValidator(db)],
            [new NoOpJobHandler(JobTypeCodes.SpreadsheetImport)],
            publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnqueueAsync(
            JobTypeCodes.SpreadsheetImport,
            new SpreadsheetImportJobPayload(999, "clientes.csv", ImportFileTypeCodes.Customers),
            userId: null,
            CancellationToken.None));

        Assert.Empty(await db.Jobs.ToListAsync());
        Assert.Empty(publisher.Published);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"job-service-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private sealed class NoOpJobHandler(string jobType) : IJobHandler
    {
        public string JobType { get; } = jobType;

        public Task HandleAsync(Job job, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPublisher : IProcessingEventPublisher
    {
        public List<ProcessingEventEnvelope> Published { get; } = [];

        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
