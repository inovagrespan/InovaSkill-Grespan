using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;

namespace InovaSkill.Importer.Infrastructure.Processing.EventHandlers;

public sealed class SummaryGenerationRequestedEventHandler(
    IEnumerable<IPostImportProcessor> processors,
    IProcessingEventPublisher eventPublisher) : IProcessingEventHandler
{
    public string EventType => ProcessingEventTypes.SummaryGenerationRequested;

    public async Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!TryGetJobType(envelope.Payload, out var jobType))
        {
            throw new InvalidOperationException("SummaryGenerationRequestedEvent sem jobType.");
        }

        var processor = processors.FirstOrDefault(x => x.JobType == jobType);
        if (processor is null)
        {
            throw new InvalidOperationException($"No post-import processor configured for job type {jobType}.");
        }

        await processor.ProcessAsync(envelope.JobId, cancellationToken);
        await eventPublisher.PublishAsync(
            ProcessingEventEnvelope.Create(ProcessingEventTypes.AnalyticsRefreshRequested, envelope.JobId, correlationId: envelope.CorrelationId),
            cancellationToken);
    }

    private static bool TryGetJobType(JsonElement? payload, out PostImportJobType jobType)
    {
        jobType = default;
        if (payload is null)
        {
            return false;
        }

        if (!payload.Value.TryGetProperty("jobType", out var jobTypeElement))
        {
            return false;
        }

        return Enum.TryParse(jobTypeElement.GetString(), ignoreCase: true, out jobType);
    }
}
