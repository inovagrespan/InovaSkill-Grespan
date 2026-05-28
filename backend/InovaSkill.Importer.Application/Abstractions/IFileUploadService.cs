namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileUploadService
{
    Task<long> UploadAndCreateJobAsync(Stream stream, string originalFileName, CancellationToken cancellationToken);
}
