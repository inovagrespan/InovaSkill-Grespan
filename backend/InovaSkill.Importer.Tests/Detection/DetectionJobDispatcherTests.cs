using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Detection;

namespace InovaSkill.Importer.Tests.Detection;

public sealed class DetectionJobDispatcherTests
{
    [Fact]
    public void Enqueue_CreatesJobInDetectorsQueue()
    {
        var client = new CapturingBackgroundJobClient();
        var dispatcher = new HangfireDetectionJobDispatcher(client);
        var runId = Guid.NewGuid();

        var hangfireJobId = dispatcher.Enqueue(runId);

        Assert.Equal(client.CreatedJobId, hangfireJobId);
        Assert.Equal(BackgroundJobQueues.Detectors, Assert.IsType<EnqueuedState>(client.State).Queue);
        Assert.Equal(typeof(ExecuteDetectionRunJob), client.Job!.Type);
        Assert.Equal(nameof(ExecuteDetectionRunJob.ExecuteAsync), client.Job.Method.Name);
        Assert.Collection(
            client.Job.Args,
            argument => Assert.Equal(runId, argument),
            argument => Assert.IsType<CancellationToken>(argument));
    }

    private sealed class CapturingBackgroundJobClient : IBackgroundJobClient
    {
        public string CreatedJobId { get; } = "hangfire-job-detection-1";
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
}
