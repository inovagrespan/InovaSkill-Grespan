namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileImportPipelineProcessor
{
    Task ProcessNextPendingJobAsync(CancellationToken cancellationToken);
}
