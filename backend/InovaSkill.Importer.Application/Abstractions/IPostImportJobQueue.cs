namespace InovaSkill.Importer.Application.Abstractions;

public interface IPostImportJobQueue
{
    Task EnqueueAsync(PostImportJobItem job, CancellationToken cancellationToken);
    Task<PostImportJobItem?> DequeueAsync(CancellationToken cancellationToken);
}
