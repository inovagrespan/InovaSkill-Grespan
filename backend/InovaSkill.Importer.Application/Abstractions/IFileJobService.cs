namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileJobService
{
    Task<long> CreateFileJobAsync(string filePath, string originalFileName, CancellationToken cancellationToken);
}
