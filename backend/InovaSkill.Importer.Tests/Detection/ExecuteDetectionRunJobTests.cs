using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Infrastructure.Detection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Detection;

public sealed class ExecuteDetectionRunJobTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToDetectionRunService()
    {
        var service = new CapturingDetectionRunService();
        var job = new ExecuteDetectionRunJob(service, NullLogger<ExecuteDetectionRunJob>.Instance);
        var runId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(runId, cancellationTokenSource.Token);

        Assert.Equal(runId, service.DetectionRunId);
        Assert.Equal(cancellationTokenSource.Token, service.CancellationToken);
    }

    private sealed class CapturingDetectionRunService : IDetectionRunService
    {
        public Guid DetectionRunId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task ExecuteAsync(Guid detectionRunId, CancellationToken cancellationToken)
        {
            DetectionRunId = detectionRunId;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
