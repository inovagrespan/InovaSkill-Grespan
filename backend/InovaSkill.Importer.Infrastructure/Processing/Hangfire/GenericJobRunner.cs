using System.Text.Json;
using Hangfire;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Processing.Hangfire;

public sealed class GenericJobRunner(
    ImportDbContext dbContext,
    ILogger<GenericJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300])]
    [Queue("default")]
    public async Task RunAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Generic job {JobId} not found.", jobId);
            return;
        }

        if (job.Status != JobStatus.Queued)
        {
            logger.LogInformation("Skipping job {JobId} with status {Status}.", job.Id, job.Status);
            return;
        }

        job.MarkProcessing();
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            job.UpdateProgress("Processando", 50);

            var payload = JsonSerializer.Deserialize<JsonElement>(job.PayloadJson, JsonOptions);
            var type = job.Type?.ToLowerInvariant() ?? "";

            switch (type)
            {
                case "ping":
                    var message = payload.TryGetProperty("message", out var msg) ? msg.GetString() : "pong";
                    job.MarkCompleted(JsonSerializer.Serialize(new { result = message }, JsonOptions));
                    break;

                case "echo":
                    job.MarkCompleted(JsonSerializer.Serialize(new { echoed = payload }, JsonOptions));
                    break;

                case "fail-test":
                    throw new InvalidOperationException("Falha induzida para teste.");
                default:
                    job.MarkCompleted(JsonSerializer.Serialize(new { processed = true, type, payload }, JsonOptions));
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Generic job {JobId} completed with type {Type}.", job.Id, type);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Generic job {JobId} failed.", job.Id);
            job.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
