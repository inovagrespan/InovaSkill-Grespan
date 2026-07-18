using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class HangfireBackgroundJobTests
{
    [Fact]
    public void EnqueueImport_CreatesJobInImportsQueueWithIdentifiersOnly()
    {
        var client = new CapturingBackgroundJobClient();
        var dispatcher = new HangfireBackgroundJobDispatcher(client);
        var importId = Guid.NewGuid();
        var jobExecutionId = Guid.NewGuid();

        var hangfireJobId = dispatcher.EnqueueImport(importId, jobExecutionId);

        Assert.Equal(client.CreatedJobId, hangfireJobId);
        Assert.Equal(BackgroundJobQueues.Imports, Assert.IsType<EnqueuedState>(client.State).Queue);
        Assert.Equal(typeof(ProcessImportJob), client.Job!.Type);
        Assert.Equal(nameof(ProcessImportJob.ExecuteAsync), client.Job.Method.Name);
        Assert.Collection(
            client.Job.Args,
            argument => Assert.Equal(importId, argument),
            argument => Assert.Equal(jobExecutionId, argument),
            argument => Assert.IsType<CancellationToken>(argument));
    }

    [Fact]
    public async Task ProcessImportJob_DelegatesToProcessingServiceWithImportAndJobIds()
    {
        var service = new CapturingImportProcessingService();
        var job = new ProcessImportJob(service, NullLogger<ProcessImportJob>.Instance);
        var importId = Guid.NewGuid();
        var jobExecutionId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(importId, jobExecutionId, cancellationTokenSource.Token);

        Assert.Equal(importId, service.ImportId);
        Assert.Equal(jobExecutionId, service.JobExecutionId);
        Assert.Equal(cancellationTokenSource.Token, service.CancellationToken);
    }

    [Fact]
    public void EnqueueOperationalJob_CreatesJobInDefaultQueueWithJobExecutionIdOnly()
    {
        var client = new CapturingBackgroundJobClient();
        var dispatcher = new HangfireBackgroundJobDispatcher(client);
        var jobExecutionId = Guid.NewGuid();

        dispatcher.EnqueueOperationalJob(jobExecutionId);

        Assert.Equal(BackgroundJobQueues.Default, Assert.IsType<EnqueuedState>(client.State).Queue);
        Assert.Equal(typeof(ProcessOperationalJob), client.Job!.Type);
        Assert.Equal(nameof(ProcessOperationalJob.ExecuteAsync), client.Job.Method.Name);
        Assert.Collection(
            client.Job.Args,
            argument => Assert.Equal(jobExecutionId, argument),
            argument => Assert.IsType<CancellationToken>(argument));
    }

    [Fact]
    public async Task ProcessOperationalJob_DelegatesToProcessingServiceWithJobExecutionId()
    {
        var service = new CapturingOperationalJobProcessingService();
        var job = new ProcessOperationalJob(service, NullLogger<ProcessOperationalJob>.Instance);
        var jobExecutionId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(jobExecutionId, cancellationTokenSource.Token);

        Assert.Equal(jobExecutionId, service.JobExecutionId);
        Assert.Equal(cancellationTokenSource.Token, service.CancellationToken);
    }

    private sealed class CapturingBackgroundJobClient : IBackgroundJobClient
    {
        public string CreatedJobId { get; } = "hangfire-job-1";

        public Job? Job { get; private set; }

        public IState? State { get; private set; }

        public string Create(Job job, IState state)
        {
            Job = job;
            State = state;
            return CreatedJobId;
        }

        public bool ChangeState(string jobId, IState state, string expectedState) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingImportProcessingService : IImportProcessingService
    {
        public Guid ImportId { get; private set; }

        public Guid JobExecutionId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task ProcessAsync(
            Guid importId,
            Guid jobExecutionId,
            CancellationToken cancellationToken)
        {
            ImportId = importId;
            JobExecutionId = jobExecutionId;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingOperationalJobProcessingService : IOperationalJobProcessingService
    {
        public Guid JobExecutionId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task ProcessAsync(
            Guid jobExecutionId,
            CancellationToken cancellationToken)
        {
            JobExecutionId = jobExecutionId;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
