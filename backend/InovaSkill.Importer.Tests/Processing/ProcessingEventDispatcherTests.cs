using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class ProcessingEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_CallsMatchingHandlerAndLogsProcessedEvent()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob { Id = 10, FilePath = "file.csv", OriginalFileName = "file.csv" });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler(ProcessingEventTypes.FileUploaded);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([handler], queue, queue, db);
        var envelope = ProcessingEventEnvelope.Create(ProcessingEventTypes.FileUploaded, 10);

        await dispatcher.DispatchAsync(envelope, CancellationToken.None);

        Assert.Single(handler.Handled);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("processed", log.Status);
        Assert.Equal(ProcessingEventTypes.FileUploaded, log.EventType);
    }

    [Fact]
    public async Task DispatchAsync_RequeuesTransientFailureUntilRetryLimit()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob { Id = 20, FilePath = "file.csv", OriginalFileName = "file.csv" });
        await db.SaveChangesAsync();
        var handler = new FailingHandler(ProcessingEventTypes.ImportRequested, new TimeoutException("temporary"));
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([handler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.ImportRequested, 20), CancellationToken.None);

        var retry = Assert.Single(queue.Published);
        Assert.Equal(1, retry.RetryCount);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("retry_scheduled", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_SendsToDeadLetterAfterRetryLimit()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob { Id = 30, FilePath = "file.csv", OriginalFileName = "file.csv" });
        await db.SaveChangesAsync();
        var handler = new FailingHandler(ProcessingEventTypes.ImportRequested, new TimeoutException("temporary"));
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([handler], queue, queue, db);
        var envelope = ProcessingEventEnvelope.Create(ProcessingEventTypes.ImportRequested, 30) with { RetryCount = 2 };

        await dispatcher.DispatchAsync(envelope, CancellationToken.None);

        Assert.Empty(queue.Published);
        Assert.Single(queue.DeadLetters);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("dead_letter", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_SkipsLockedJobWithoutCallingHandler()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 40,
            FilePath = "file.csv",
            OriginalFileName = "file.csv",
            LockedBy = "other-worker",
            LockedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler(ProcessingEventTypes.FileUploaded);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([handler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.FileUploaded, 40), CancellationToken.None);

        Assert.Empty(handler.Handled);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("skipped", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_MovesUnknownEventToDeadLetterWithoutClaimingJob()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob { Id = 50, FilePath = "file.csv", OriginalFileName = "file.csv" });
        await db.SaveChangesAsync();
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([], queue, queue, db);
        var envelope = ProcessingEventEnvelope.Create("UnknownEvent", 50);

        await dispatcher.DispatchAsync(envelope, CancellationToken.None);

        Assert.Empty(queue.Published);
        var deadLetter = Assert.Single(queue.DeadLetters);
        Assert.Equal(envelope.CorrelationId, deadLetter.CorrelationId);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("dead_letter", log.Status);
        Assert.Contains("No handler", log.ErrorMessage);
        var job = await db.FileJobs.SingleAsync(x => x.Id == 50);
        Assert.True(string.IsNullOrEmpty(job.LockedBy));
        Assert.Null(job.LockedAt);
    }

    [Fact]
    public async Task DispatchAsync_ReclaimsStaleLockAndReleasesItAfterProcessing()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 60,
            FilePath = "file.csv",
            OriginalFileName = "file.csv",
            LockedBy = "dead-worker",
            LockedAt = DateTime.UtcNow.AddMinutes(-11)
        });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler(ProcessingEventTypes.FileUploaded);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([handler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.FileUploaded, 60), CancellationToken.None);

        Assert.Single(handler.Handled);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("processed", log.Status);
        var job = await db.FileJobs.SingleAsync(x => x.Id == 60);
        Assert.True(string.IsNullOrEmpty(job.LockedBy));
        Assert.Null(job.LockedAt);
    }

    [Fact]
    public async Task DispatchAsync_GenericJobCallsMatchingJobHandlerAndMarksCompleted()
    {
        await using var db = CreateDb();
        db.Jobs.Add(new Job
        {
            Id = 100,
            Type = JobTypeCodes.SpreadsheetImport,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync();
        var jobHandler = new RecordingJobHandler(JobTypeCodes.SpreadsheetImport);
        var eventHandler = new JobRequestedEventHandler(db, [jobHandler]);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([eventHandler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.JobRequested, 100), CancellationToken.None);

        Assert.Single(jobHandler.Handled);
        var job = await db.Jobs.SingleAsync(x => x.Id == 100);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(100, job.ProgressPercent);
        Assert.True(string.IsNullOrEmpty(job.LockedBy));
        Assert.Null(job.LockedAt);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("processed", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_GenericJobRequeuesTransientFailureAndStoresRetryState()
    {
        await using var db = CreateDb();
        db.Jobs.Add(new Job
        {
            Id = 110,
            Type = JobTypeCodes.SpreadsheetImport,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync();
        var eventHandler = new JobRequestedEventHandler(
            db,
            [new FailingJobHandler(JobTypeCodes.SpreadsheetImport, new TimeoutException("temporary"))]);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([eventHandler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.JobRequested, 110), CancellationToken.None);

        var retry = Assert.Single(queue.Published);
        Assert.Equal(1, retry.RetryCount);
        var job = await db.Jobs.SingleAsync(x => x.Id == 110);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal("temporary", job.Error);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("retry_scheduled", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_GenericJobSendsToDeadLetterAfterRetryLimitAndMarksFailed()
    {
        await using var db = CreateDb();
        db.Jobs.Add(new Job
        {
            Id = 120,
            Type = JobTypeCodes.SpreadsheetImport,
            Status = JobStatus.Queued,
            RetryCount = 2,
            PayloadJson = "{}"
        });
        await db.SaveChangesAsync();
        var eventHandler = new JobRequestedEventHandler(
            db,
            [new FailingJobHandler(JobTypeCodes.SpreadsheetImport, new TimeoutException("temporary"))]);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([eventHandler], queue, queue, db);
        var envelope = ProcessingEventEnvelope.Create(ProcessingEventTypes.JobRequested, 120) with { RetryCount = 2 };

        await dispatcher.DispatchAsync(envelope, CancellationToken.None);

        Assert.Empty(queue.Published);
        Assert.Single(queue.DeadLetters);
        var job = await db.Jobs.SingleAsync(x => x.Id == 120);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("temporary", job.Error);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("dead_letter", log.Status);
    }

    [Fact]
    public async Task DispatchAsync_GenericJobSkipsLockedJobWithoutCallingHandler()
    {
        await using var db = CreateDb();
        db.Jobs.Add(new Job
        {
            Id = 130,
            Type = JobTypeCodes.SpreadsheetImport,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            LockedBy = "other-worker",
            LockedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var jobHandler = new RecordingJobHandler(JobTypeCodes.SpreadsheetImport);
        var eventHandler = new JobRequestedEventHandler(db, [jobHandler]);
        var queue = new RecordingEventQueue();
        var dispatcher = new ProcessingEventDispatcher([eventHandler], queue, queue, db);

        await dispatcher.DispatchAsync(ProcessingEventEnvelope.Create(ProcessingEventTypes.JobRequested, 130), CancellationToken.None);

        Assert.Empty(jobHandler.Handled);
        var job = await db.Jobs.SingleAsync(x => x.Id == 130);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("other-worker", job.LockedBy);
        var log = await db.ProcessingJobEventLogs.SingleAsync();
        Assert.Equal("skipped", log.Status);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"event-dispatcher-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private sealed class RecordingHandler(string eventType) : IProcessingEventHandler
    {
        public string EventType { get; } = eventType;
        public List<ProcessingEventEnvelope> Handled { get; } = [];

        public Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Handled.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler(string eventType, Exception exception) : IProcessingEventHandler
    {
        public string EventType { get; } = eventType;

        public Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class RecordingJobHandler(string jobType) : IJobHandler
    {
        public string JobType { get; } = jobType;
        public List<Job> Handled { get; } = [];

        public Task HandleAsync(Job job, CancellationToken cancellationToken)
        {
            Handled.Add(job);
            job.UpdateProgress("Executando job", 50);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingJobHandler(string jobType, Exception exception) : IJobHandler
    {
        public string JobType { get; } = jobType;

        public Task HandleAsync(Job job, CancellationToken cancellationToken)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class RecordingEventQueue : IProcessingEventPublisher, IProcessingDeadLetterQueue
    {
        public List<ProcessingEventEnvelope> Published { get; } = [];
        public List<ProcessingEventEnvelope> DeadLetters { get; } = [];

        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add(envelope);
            return Task.CompletedTask;
        }

        public Task PublishDeadLetterAsync(ProcessingEventEnvelope envelope, string reason, CancellationToken cancellationToken)
        {
            DeadLetters.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
