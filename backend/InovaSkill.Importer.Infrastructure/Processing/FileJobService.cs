using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileJobService(ImportDbContext dbContext) : IFileJobService
{
    public async Task<long> CreateFileJobAsync(string filePath, string originalFileName, CancellationToken cancellationToken)
    {
        var job = new FileJob
        {
            FilePath = filePath,
            OriginalFileName = originalFileName,
            Status = FileJobStatus.WaitingProcessing,
            CreatedAt = DateTime.UtcNow,
            LastHeartbeatAt = DateTime.UtcNow
        };

        dbContext.FileJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }
}
