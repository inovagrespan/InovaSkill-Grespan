using InovaSkill.Importer.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileUploadService(IFileJobService fileJobService, IConfiguration configuration) : IFileUploadService
{
    public async Task<long> UploadAndCreateJobAsync(Stream stream, string originalFileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx")
        {
            throw new InvalidOperationException("Only .csv and .xlsx are supported.");
        }

        var configuredUploadsPath = configuration["Storage:UploadsPath"];
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var uploadsDir = string.IsNullOrWhiteSpace(configuredUploadsPath)
            ? Path.Combine(repositoryRoot, "uploads")
            : Path.GetFullPath(configuredUploadsPath);
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, $"{Guid.NewGuid():N}{extension}");
        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return await fileJobService.CreateFileJobAsync(filePath, cancellationToken);
    }
}
