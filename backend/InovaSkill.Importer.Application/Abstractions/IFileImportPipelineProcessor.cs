namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileImportPipelineProcessor
{
    Task ProcessJobAsync(long jobId, CancellationToken cancellationToken);
}
