namespace InovaSkill.Importer.Application.Abstractions;

public interface IJobService
{
    Task<long> EnqueueAsync(
        string type,
        object payload,
        string? userId,
        CancellationToken cancellationToken);
}
