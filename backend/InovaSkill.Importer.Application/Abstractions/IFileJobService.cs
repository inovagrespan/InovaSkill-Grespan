namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileJobService
{
    Task<long> CreateFileJobAsync(
        string filePath,
        string originalFileName,
        string? importFileTypeCode,
        CancellationToken cancellationToken);
}
