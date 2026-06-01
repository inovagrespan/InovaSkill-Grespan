using InovaSkill.Importer.Application.Events;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IProcessingEventPublisher
{
    Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken);
}

public interface IProcessingEventConsumer
{
    Task<ProcessingEventEnvelope?> DequeueAsync(CancellationToken cancellationToken);
}

public interface IProcessingDeadLetterQueue
{
    Task PublishDeadLetterAsync(ProcessingEventEnvelope envelope, string reason, CancellationToken cancellationToken);
}

public interface IProcessingEventQueue : IProcessingEventPublisher, IProcessingEventConsumer, IProcessingDeadLetterQueue;
