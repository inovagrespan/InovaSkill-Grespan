using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Worker;

public sealed class FileImportHangfireJob(IFileImportPipelineProcessor processor, ILogger<FileImportHangfireJob> logger)
{
    public async Task ProcessOnePendingFileJob()
    {
        try
        {
            await processor.ProcessNextPendingJobAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while processing pending file jobs via Hangfire");
            throw;
        }
    }
}
