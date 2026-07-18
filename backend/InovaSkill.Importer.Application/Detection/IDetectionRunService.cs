namespace InovaSkill.Importer.Application.Detection;

public interface IDetectionRunService
{
    Task ExecuteAsync(Guid detectionRunId, CancellationToken cancellationToken);
}
