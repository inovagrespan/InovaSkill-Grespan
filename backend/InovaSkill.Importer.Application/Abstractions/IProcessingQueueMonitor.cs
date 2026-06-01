namespace InovaSkill.Importer.Application.Abstractions;

public interface IProcessingQueueMonitor
{
    Task<ProcessingQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

public sealed record ProcessingQueueSnapshot(int ImportQueueLength, int PostImportQueueLength);
