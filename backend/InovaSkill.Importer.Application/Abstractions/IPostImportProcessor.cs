namespace InovaSkill.Importer.Application.Abstractions;

public interface IPostImportProcessor
{
    PostImportJobType JobType { get; }
    Task ProcessAsync(long fileJobId, CancellationToken cancellationToken);
}
