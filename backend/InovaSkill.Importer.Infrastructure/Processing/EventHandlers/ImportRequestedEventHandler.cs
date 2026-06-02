using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;

namespace InovaSkill.Importer.Infrastructure.Processing.EventHandlers;

public sealed class ImportRequestedEventHandler(IFileImportPipelineProcessor processor) : IProcessingEventHandler
{
    public string EventType => ProcessingEventTypes.ImportRequested;

    public Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        return processor.ProcessJobAsync(envelope.JobId, cancellationToken);
    }
}
