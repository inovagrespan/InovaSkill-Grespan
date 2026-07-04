using System.Text.Json;
using Hangfire;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Analytics;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InovaSkill.Importer.Infrastructure.Processing.Hangfire;

public sealed class SpreadsheetImportJobRunner(
    IFileImportPipelineProcessor processor,
    ImportDbContext dbContext,
    IBackgroundJobClient backgroundJobClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300])]
    public async Task RunAsync(long fileJobId, CancellationToken cancellationToken)
    {
        var fileJob = await dbContext.FileJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fileJobId, cancellationToken);
        if (fileJob is null) return;

        if (fileJob.Status != FileJobStatus.WaitingProcessing && fileJob.Status != FileJobStatus.ReadyToImport)
            return;

        await SafeUpdateJobStatusAsync(fileJobId, "Processando", cancellationToken);
        await processor.ProcessJobAsync(fileJobId, cancellationToken);

        var updated = await dbContext.FileJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fileJobId, cancellationToken);
        if (updated is null) return;

        if (updated.Status is FileJobStatus.Completed or FileJobStatus.ValidationFailed)
        {
            await SafeUpdateJobStatusAsync(fileJobId, "Concluído", cancellationToken, completed: true);

            if (updated.Status == FileJobStatus.Completed)
            {
                if (string.Equals(updated.ImportFileTypeCode, "SALES_INVOICE", StringComparison.OrdinalIgnoreCase))
                {
                    backgroundJobClient.Enqueue<SalesSummaryJobRunner>(x => x.RunAsync(fileJobId, CancellationToken.None));
                    backgroundJobClient.Enqueue<CustomerSummaryJobRunner>(x => x.RunAsync(fileJobId, CancellationToken.None));
                }

                // Refresh analytics materialized view and recalculate indicators
                backgroundJobClient.Enqueue<RefreshMetricasJob>(x => x.ExecutarAsync(CancellationToken.None));
                backgroundJobClient.Enqueue<ClienteIndicadoresJob>(x => x.ExecutarAsync(CancellationToken.None));
                backgroundJobClient.Enqueue<ForecastWorker>(x => x.ExecutarAsync(CancellationToken.None));
            }
        }
        else if (updated.Status == FileJobStatus.Failed)
        {
            await SafeUpdateJobStatusAsync(fileJobId, updated.CurrentStep, cancellationToken, failed: true);
        }
    }

    private async Task SafeUpdateJobStatusAsync(long fileJobId, string step, CancellationToken ct, bool completed = false, bool failed = false)
    {
        try
        {
            var connStr = dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return;

            var jsonSearch = $"%\"fileJobId\": {fileJobId}%";
            var newStatus = completed ? JobStatus.Completed : failed ? JobStatus.Failed : JobStatus.Processing;
            var now = DateTime.UtcNow;

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(
                @"UPDATE ""Jobs"" SET ""Status"" = @status, ""CurrentStep"" = @step, ""ProgressPercent"" = @progress,
                  ""FinishedAt"" = @finished, ""StartedAt"" = COALESCE(""StartedAt"", @now)
                  WHERE ""Type"" = @type AND ""PayloadJson""::text LIKE @search", conn);
            cmd.Parameters.AddWithValue("@status", (int)newStatus);
            cmd.Parameters.AddWithValue("@step", step);
            cmd.Parameters.AddWithValue("@progress", completed || failed ? 100 : 50);
            cmd.Parameters.AddWithValue("@finished", completed || failed ? now : DBNull.Value);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@type", "SpreadsheetImport");
            cmd.Parameters.AddWithValue("@search", jsonSearch);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // best-effort — pipeline still runs
        }
    }
}

public sealed class SalesSummaryJobRunner(IPostImportProcessor processor)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300])]
    [Queue("summary")]
    public async Task RunAsync(long fileJobId, CancellationToken cancellationToken)
    {
        await processor.ProcessAsync(fileJobId, cancellationToken);
    }
}

public sealed class CustomerSummaryJobRunner(IPostImportProcessor processor)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 300])]
    [Queue("summary")]
    public async Task RunAsync(long fileJobId, CancellationToken cancellationToken)
    {
        await processor.ProcessAsync(fileJobId, cancellationToken);
    }
}
