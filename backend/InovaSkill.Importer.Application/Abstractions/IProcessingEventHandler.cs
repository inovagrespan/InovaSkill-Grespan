using InovaSkill.Importer.Application.Events;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IProcessingEventHandler
{
    string EventType { get; }

    Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken);
}
