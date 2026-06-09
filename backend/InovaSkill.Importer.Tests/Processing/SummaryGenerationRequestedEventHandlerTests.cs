using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Infrastructure.Processing.EventHandlers;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class SummaryGenerationRequestedEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_ProcessesMatchingSummaryJobAndPublishesAnalyticsRefresh()
    {
        var processor = new RecordingPostImportProcessor(PostImportJobType.CustomerSummary);
        var publisher = new RecordingProcessingEventPublisher();
        var handler = new SummaryGenerationRequestedEventHandler([processor], publisher);
        var correlationId = Guid.NewGuid();
        var envelope = ProcessingEventEnvelope.Create(
            ProcessingEventTypes.SummaryGenerationRequested,
            91,
            JsonSerializer.SerializeToElement(new { jobType = nameof(PostImportJobType.CustomerSummary) }),
            correlationId: correlationId);

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal([91L], processor.ProcessedJobIds);
        var refresh = Assert.Single(publisher.Published);
        Assert.Equal(ProcessingEventTypes.AnalyticsRefreshRequested, refresh.EventType);
        Assert.Equal(91, refresh.JobId);
        Assert.Equal(correlationId, refresh.CorrelationId);
    }

    [Fact]
    public async Task HandleAsync_ThrowsWhenPayloadDoesNotInformJobType()
    {
        var handler = new SummaryGenerationRequestedEventHandler([], new RecordingProcessingEventPublisher());
        var envelope = ProcessingEventEnvelope.Create(
            ProcessingEventTypes.SummaryGenerationRequested,
            92,
            JsonSerializer.SerializeToElement(new { foo = "bar" }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(envelope, CancellationToken.None));

        Assert.Contains("jobType", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingPostImportProcessor(PostImportJobType jobType) : IPostImportProcessor
    {
        public PostImportJobType JobType { get; } = jobType;
        public List<long> ProcessedJobIds { get; } = [];

        public Task ProcessAsync(long fileJobId, CancellationToken cancellationToken)
        {
            ProcessedJobIds.Add(fileJobId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcessingEventPublisher : IProcessingEventPublisher
    {
        public List<ProcessingEventEnvelope> Published { get; } = [];

        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
