using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using Microsoft.Extensions.Configuration;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileUploadService(
    IFileJobService fileJobService,
    IProcessingEventPublisher eventPublisher,
    IConfiguration configuration) : IFileUploadService
{
    public async Task<long> UploadAndCreateJobAsync(
        Stream stream,
        string originalFileName,
        string? importFileTypeCode,
        CancellationToken cancellationToken)
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

        var fileName = BuildStoredFileName(originalFileName, extension);
        var filePath = Path.Combine(uploadsDir, fileName);
        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, cancellationToken);

        var jobId = await fileJobService.CreateFileJobAsync(filePath, originalFileName, importFileTypeCode, cancellationToken);
        await eventPublisher.PublishAsync(
            ProcessingEventEnvelope.Create(ProcessingEventTypes.FileUploaded, jobId),
            cancellationToken);
        return jobId;
    }

    private static string BuildStoredFileName(string originalFileName, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "arquivo";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "arquivo";
        }

        var salt = Guid.NewGuid().ToString("N")[..5];
        return $"{sanitized}_{salt}{extension}";
    }
}
