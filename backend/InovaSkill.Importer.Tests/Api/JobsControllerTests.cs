using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class JobsControllerTests
{
    [Fact]
    public async Task Retry_WhenSpreadsheetImportFailed_RequeuesFileJobAndEnqueuesHangfire()
    {
        await using var db = CreateDb();
        var client = new StubBackgroundJobClient();

        var fileJob = new FileJob
        {
            Id = 100,
            FilePath = "clientes.csv",
            OriginalFileName = "clientes.csv",
            Status = FileJobStatus.Failed,
            CurrentStep = "Falha no processamento"
        };
        db.FileJobs.Add(fileJob);

        var job = new Domain.Entities.Job
        {
            Type = "SpreadsheetImport",
            Status = JobStatus.Failed,
            PayloadJson = """{"fileJobId": 100, "originalFileName": "clientes.csv"}""",
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = new JobsController(db, new StubJobService(), client);

        var result = await controller.Retry(job.Id, new EnqueueGenericJobRequest(null, null, null), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var reloadedJob = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Queued, reloadedJob.Status);

        var reloadedFileJob = await db.FileJobs.AsNoTracking().SingleAsync(f => f.Id == fileJob.Id);
        Assert.Equal(FileJobStatus.WaitingProcessing, reloadedFileJob.Status);
        Assert.Equal("Reenfileirado manualmente", reloadedFileJob.CurrentStep);

        Assert.NotEmpty(client.CreatedJobs);
    }

    [Fact]
    public async Task Retry_WhenGenericJobFailed_UpdatesStatusAndEnqueues()
    {
        await using var db = CreateDb();
        var client = new StubBackgroundJobClient();

        var job = new Domain.Entities.Job
        {
            Type = "ping",
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = new JobsController(db, new StubJobService(), client);

        var result = await controller.Retry(job.Id, new EnqueueGenericJobRequest(null, null, null), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var reloadedJob = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Queued, reloadedJob.Status);

        Assert.NotEmpty(client.CreatedJobs);
    }

    [Fact]
    public async Task Cancel_WhenSpreadsheetImport_CancelsBothJobAndFileJob()
    {
        await using var db = CreateDb();
        var client = new StubBackgroundJobClient();

        var fileJob = new FileJob
        {
            Id = 200,
            FilePath = "vendas.csv",
            OriginalFileName = "vendas.csv",
            Status = FileJobStatus.Importing
        };
        db.FileJobs.Add(fileJob);

        var job = new Domain.Entities.Job
        {
            Type = "SpreadsheetImport",
            Status = JobStatus.Processing,
            PayloadJson = """{"fileJobId": 200, "originalFileName": "vendas.csv"}""",
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = new JobsController(db, new StubJobService(), client);

        var result = await controller.Cancel(job.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var reloadedJob = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Cancelled, reloadedJob.Status);

        var reloadedFileJob = await db.FileJobs.AsNoTracking().SingleAsync(f => f.Id == fileJob.Id);
        Assert.Equal(FileJobStatus.Cancelled, reloadedFileJob.Status);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"jobs-controller-{Guid.NewGuid():N}")
            .Options;
        return new ImportDbContext(options);
    }

    private sealed class StubBackgroundJobClient : IBackgroundJobClient
    {
        public List<Hangfire.Common.Job> CreatedJobs { get; } = [];

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;

        public string Create(Hangfire.Common.Job job, IState state)
        {
            CreatedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool Delete(string jobId, string? fromState) => true;
        public bool IsJobDeleted(string jobId, string? fromState) => false;
        public bool SetJobParameter(string jobId, string name, string value) => true;
        public string GetJobParameter(string jobId, string name) => string.Empty;
    }

    private sealed class StubJobService : IJobService
    {
        public Task<long> EnqueueAsync(string type, object payload, string? userId, CancellationToken cancellationToken)
            => Task.FromResult(0L);
    }
}
