namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileJobQueue
{
    Task EnqueueAsync(long fileJobId, CancellationToken cancellationToken);
    Task<long?> DequeueAsync(CancellationToken cancellationToken);
}
