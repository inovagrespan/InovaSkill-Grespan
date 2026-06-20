using System.Text;
using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Api.Presentation;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/processing-monitoring")]
public sealed class ProcessingMonitoringController(
    ImportDbContext dbContext,
    IProcessingQueueMonitor queueMonitor,
    IJobService jobService,
    IPostImportJobQueue postImportJobQueue) : ControllerBase
{
    private static readonly TimeSpan StaleJobTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WorkerOfflineTimeout = TimeSpan.FromSeconds(30);

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProcessingMonitoringDashboardDto>> GetDashboard(CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var now = DateTime.UtcNow;
        var today = now.Date;
        var historyStart = today.AddDays(-29);
        var queue = await queueMonitor.GetSnapshotAsync(cancellationToken);

        var jobs = await dbContext.FileJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var historyJobs = await dbContext.FileJobs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= historyStart)
            .ToListAsync(cancellationToken);

        var jobIds = jobs.Select(x => x.Id).ToArray();
        var errorCounts = await GetErrorCountsAsync(jobIds, cancellationToken);

        var stageExecutions = await dbContext.ProcessingStepExecutions
            .AsNoTracking()
            .Where(x => x.StartedAt >= historyStart && x.FinishedAt != null)
            .ToListAsync(cancellationToken);

        var workers = await dbContext.WorkerHeartbeats
            .AsNoTracking()
            .OrderBy(x => x.WorkerId)
            .ToListAsync(cancellationToken);

        var summary = BuildSummary(historyJobs, queue, now, today);
        var stageErrorCounts = await GetStageErrorCountsAsync(jobIds, cancellationToken);
        var items = jobs.Select(job => BuildJobItem(job, errorCounts, stageErrorCounts, now)).ToList();
        var daily = BuildDailyPoints(historyJobs, historyStart, today);
        var stages = BuildStageDurations(stageExecutions);
        var workerHealth = workers.Select(worker => BuildWorkerHealth(worker, now)).ToList();

        return Ok(new ProcessingMonitoringDashboardDto(summary, items, daily, stages, workerHealth));
    }

    [HttpGet("jobs/{jobId:long}")]
    public async Task<ActionResult<ProcessingJobDetailsDto>> GetJobDetails(long jobId, CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var now = DateTime.UtcNow;
        var job = await dbContext.FileJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        var errorCounts = await GetErrorCountsAsync([jobId], cancellationToken);
        var stageErrorCounts = await GetStageErrorCountsAsync([jobId], cancellationToken);
        var logs = await dbContext.ProcessingJobLogs
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId)
            .OrderByDescending(x => x.Timestamp)
            .Take(200)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(cancellationToken);

        var executions = await dbContext.ProcessingStepExecutions
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);

        var jobItem = BuildJobItem(job, errorCounts, stageErrorCounts, now);
        var timeline = BuildTimeline(job, executions);
        var metrics = BuildMetrics(job, errorCounts);
        var performance = BuildStageDurations(executions.Where(x => x.FinishedAt != null).ToList());
        var logDtos = logs.Select(x => new ProcessingLogDto(x.Timestamp, x.FileJobId, x.Stage, x.Level, x.Message)).ToList();

        return Ok(new ProcessingJobDetailsDto(jobItem, timeline, metrics, performance, logDtos));
    }

    [HttpPost("jobs/{jobId:long}/retry")]
    public async Task<ActionResult> Retry(long jobId, CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status == FileJobStatus.Importing)
        {
            return Conflict("Aguarde o job sair da importacao antes de reprocessar.");
        }

        job.RequeueManually();
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = job.Id,
            Stage = "OPERATIONS",
            Level = "Information",
            Message = "Job reenfileirado pela central de processamentos."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobService.EnqueueAsync(
            JobTypeCodes.SpreadsheetImport,
            new SpreadsheetImportJobPayload(job.Id, job.OriginalFileName, job.ImportFileTypeCode),
            userId: null,
            cancellationToken);
        return Ok();
    }

    [HttpPost("jobs/{jobId:long}/cancel")]
    public async Task<ActionResult> Cancel(long jobId, CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status is FileJobStatus.Completed or FileJobStatus.Failed or FileJobStatus.ValidationFailed or FileJobStatus.Cancelled)
        {
            return Conflict("Job nao esta em execucao ou fila.");
        }

        job.MarkCancelled();
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = job.Id,
            Stage = "OPERATIONS",
            Level = "Warning",
            Message = "Job cancelado pela central de processamentos."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("jobs/manual-actions")]
    public async Task<ActionResult<ProcessingManualActionResponseDto>> RunManualAction(
        [FromBody] ProcessingManualActionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var action = request.Action?.Trim().ToLowerInvariant();
        var jobIds = request.JobIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? [];

        if (string.IsNullOrWhiteSpace(action))
        {
            return BadRequest("Ação manual obrigatória.");
        }

        if (jobIds.Length == 0)
        {
            return BadRequest("Informe ao menos um job para execução manual.");
        }

        if (!TryResolveManualAction(action, out var jobType))
        {
            return BadRequest("Ação manual não suportada.");
        }

        var jobs = await dbContext.FileJobs
            .Where(x => jobIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (jobs.Count != jobIds.Length)
        {
            return NotFound("Um ou mais jobs informados não foram encontrados.");
        }

        var invalidJobs = jobs
            .Where(x => x.Status != FileJobStatus.Completed)
            .Select(x => x.Id)
            .ToArray();

        if (invalidJobs.Length > 0)
        {
            return Conflict($"Somente jobs concluídos podem executar ações manuais. Jobs inválidos: {string.Join(", ", invalidJobs)}");
        }

        foreach (var job in jobs)
        {
            await postImportJobQueue.EnqueueAsync(new PostImportJobItem(job.Id, jobType), cancellationToken);
            dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
            {
                FileJobId = job.Id,
                Stage = "OPERATIONS",
                Level = "Information",
                Message = $"Ação manual '{action}' enfileirada pela central de processamentos."
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ProcessingManualActionResponseDto(action, jobs.Count));
    }

    [HttpGet("jobs/{jobId:long}/logs/download")]
    public async Task<ActionResult> DownloadLogs(long jobId, CancellationToken cancellationToken = default)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var exists = await dbContext.FileJobs.AsNoTracking().AnyAsync(x => x.Id == jobId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var logs = await dbContext.ProcessingJobLogs
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        foreach (var log in logs)
        {
            builder.AppendLine($"{log.Timestamp:O}\t{log.FileJobId}\t{log.Stage}\t{log.Level}\t{log.Message}");
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/plain", $"job-{jobId}-logs.txt");
    }

    private static ProcessingMonitoringSummaryDto BuildSummary(
        IReadOnlyList<FileJob> historyJobs,
        ProcessingQueueSnapshot queue,
        DateTime now,
        DateTime today)
    {
        var runningJobs = historyJobs.Count(x => IsRunning(x.Status));
        var queuedJobs = historyJobs.Count(x => x.Status is FileJobStatus.WaitingProcessing or FileJobStatus.ReadyToImport) +
            queue.ImportQueueLength +
            queue.PostImportQueueLength;
        var completedToday = historyJobs.Count(x => x.Status == FileJobStatus.Completed && (x.FinishedAt ?? x.LastHeartbeatAt) >= today);
        var failedJobs = historyJobs.Count(x => x.Status is FileJobStatus.Failed or FileJobStatus.ValidationFailed);
        var completedDurations = historyJobs
            .Where(x => x.Status == FileJobStatus.Completed)
            .Select(GetDurationSeconds)
            .Where(x => x > 0)
            .ToArray();
        var processedRowsToday = historyJobs
            .Where(x => (x.FinishedAt ?? x.LastHeartbeatAt) >= today)
            .Sum(x => x.ProcessedRows);
        var staleJobs = historyJobs.Count(x => IsRunning(x.Status) && now - x.LastHeartbeatAt > StaleJobTimeout);

        return new ProcessingMonitoringSummaryDto(
            runningJobs,
            queuedJobs,
            completedToday,
            failedJobs,
            completedDurations.Length == 0 ? 0 : completedDurations.Average(),
            processedRowsToday,
            staleJobs);
    }

    private static IReadOnlyList<ProcessingDailyPointDto> BuildDailyPoints(IReadOnlyList<FileJob> jobs, DateTime start, DateTime end)
    {
        var points = new List<ProcessingDailyPointDto>();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            var dayJobs = jobs.Where(x => x.CreatedAt.Date == date).ToList();
            var completed = dayJobs.Count(x => x.Status == FileJobStatus.Completed);
            var failed = dayJobs.Count(x => x.Status is FileJobStatus.Failed or FileJobStatus.ValidationFailed);
            var finished = dayJobs.Where(x => x.Status is FileJobStatus.Completed or FileJobStatus.Failed or FileJobStatus.ValidationFailed).ToList();
            var avg = dayJobs.Select(GetDurationSeconds).Where(x => x > 0).DefaultIfEmpty(0).Average();
            var successRate = finished.Count == 0 ? 0 : Math.Round(100d * completed / finished.Count, 2);

            points.Add(new ProcessingDailyPointDto(
                date,
                dayJobs.Count,
                completed,
                failed,
                dayJobs.Sum(x => x.ProcessedRows),
                avg,
                successRate));
        }

        return points;
    }

    private static IReadOnlyList<ProcessingStageDurationDto> BuildStageDurations(IReadOnlyList<ProcessingStepExecution> executions)
    {
        var items = executions
            .Where(x => x.Duration.HasValue && x.Duration.Value.TotalSeconds >= 0)
            .GroupBy(x => x.Step)
            .Select(x => new
            {
                Stage = x.Key,
                Average = x.Average(item => item.Duration!.Value.TotalSeconds)
            })
            .ToList();
        var total = items.Sum(x => x.Average);

        return items
            .OrderByDescending(x => x.Average)
            .Select(x => new ProcessingStageDurationDto(
                x.Stage,
                StageName(x.Stage),
                x.Average,
                total <= 0 ? 0 : Math.Round(100d * x.Average / total, 2)))
            .ToList();
    }

    private static IReadOnlyList<ProcessingStepDto> BuildTimeline(FileJob job, IReadOnlyList<ProcessingStepExecution> executions)
    {
        var byStep = executions
            .GroupBy(x => x.Step)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(item => item.StartedAt).First());

        var steps = new[] { "UPLOAD", ImportProcessingStages.PreProcessing, ImportProcessingStages.Validation, ImportProcessingStages.Import, "SUMMARY", "COMPLETED" };
        return steps.Select(step =>
        {
            if (step == "UPLOAD")
            {
                return new ProcessingStepDto(step, StageName(step), job.CreatedAt, job.CreatedAt, 0, "completed", 0, 0);
            }

            if (step == "COMPLETED")
            {
                var isCompleted = job.Status == FileJobStatus.Completed;
                return new ProcessingStepDto(step, StageName(step), job.FinishedAt, job.FinishedAt, 0, isCompleted ? "completed" : "pending", job.ProcessedRows, 0);
            }

            return byStep.TryGetValue(step, out var execution)
                ? new ProcessingStepDto(step, StageName(step), execution.StartedAt, execution.FinishedAt, execution.Duration?.TotalSeconds ?? 0, execution.Status, execution.ProcessedRows, execution.ErrorCount)
                : new ProcessingStepDto(step, StageName(step), null, null, 0, "pending", 0, 0);
        }).ToList();
    }

    private static ProcessingJobMetricsDto BuildMetrics(FileJob job, IReadOnlyDictionary<long, int> errorCounts)
    {
        var errors = errorCounts.TryGetValue(job.Id, out var count) ? count : 0;
        var total = Math.Max(job.TotalRows, job.ProcessedRows);
        return new ProcessingJobMetricsDto(
            total,
            Math.Max(0, total - errors),
            errors,
            job.Status == FileJobStatus.Completed ? job.ProcessedRows : 0,
            errors,
            0);
    }

    private static ProcessingJobQueueItemDto BuildJobItem(
        FileJob job,
        IReadOnlyDictionary<long, int> errorCounts,
        IReadOnlyDictionary<(long FileJobId, string Stage), int> stageErrorCounts,
        DateTime now)
    {
        var errors = errorCounts.TryGetValue(job.Id, out var count) ? count : 0;
        var stages = FileJobStageProgressPresenter.Build(job, stageErrorCounts);
        var currentStage = FileJobStageProgressPresenter.ResolveCurrentStage(stages);
        return new ProcessingJobQueueItemDto(
            job.Id,
            "-",
            Path.GetFileName(job.FilePath),
            job.ImportFileTypeCode,
            NormalizeStatus(job.Status),
            StatusLabel(job.Status),
            job.CurrentStep,
            Math.Clamp(job.ProgressPercent, 0, 100),
            currentStage.Code,
            currentStage.Name,
            stages,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            GetElapsedSeconds(job, now),
            job.ProcessedRows,
            job.TotalRows,
            errors,
            job.Status == FileJobStatus.Completed);
    }

    private static WorkerHealthDto BuildWorkerHealth(WorkerHeartbeat worker, DateTime now)
    {
        var secondsSinceLastSeen = Math.Max(0, (now - worker.LastSeenAt).TotalSeconds);
        var status = secondsSinceLastSeen <= WorkerOfflineTimeout.TotalSeconds ? "Online" : "Offline";
        var idleSeconds = worker.IdleSinceAt.HasValue ? Math.Max(0, (now - worker.IdleSinceAt.Value).TotalSeconds) : 0;
        return new WorkerHealthDto(
            worker.WorkerId,
            status,
            worker.LastSeenAt,
            secondsSinceLastSeen,
            worker.ProcessedJobsToday,
            idleSeconds,
            worker.CurrentJobId,
            worker.CurrentTask);
    }

    private async Task<IReadOnlyDictionary<long, int>> GetErrorCountsAsync(long[] jobIds, CancellationToken cancellationToken)
    {
        if (jobIds.Length == 0)
        {
            return new Dictionary<long, int>();
        }

        return await dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => jobIds.Contains(x.FileJobId))
            .GroupBy(x => x.FileJobId)
            .Select(x => new { FileJobId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.FileJobId, x => x.Count, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(long FileJobId, string Stage), int>> GetStageErrorCountsAsync(long[] jobIds, CancellationToken cancellationToken)
    {
        if (jobIds.Length == 0)
        {
            return new Dictionary<(long FileJobId, string Stage), int>();
        }

        var grouped = await dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => jobIds.Contains(x.FileJobId))
            .GroupBy(x => new { x.FileJobId, x.Stage })
            .Select(x => new
            {
                x.Key.FileJobId,
                x.Key.Stage,
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        return grouped
            .GroupBy(x => (x.FileJobId, NormalizeStageCode(x.Stage)))
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));
    }

    private static bool IsRunning(FileJobStatus status)
    {
        return status is FileJobStatus.PreProcessing or FileJobStatus.Validating or FileJobStatus.Importing;
    }

    private static double GetDurationSeconds(FileJob job)
    {
        var startedAt = job.StartedAt ?? job.CreatedAt;
        var finishedAt = job.FinishedAt ?? (job.Status is FileJobStatus.Completed or FileJobStatus.Failed or FileJobStatus.ValidationFailed or FileJobStatus.Cancelled ? job.LastHeartbeatAt : null);
        return finishedAt.HasValue ? Math.Max(0, (finishedAt.Value - startedAt).TotalSeconds) : 0;
    }

    private static double GetElapsedSeconds(FileJob job, DateTime now)
    {
        var startedAt = job.StartedAt ?? job.CreatedAt;
        var end = job.FinishedAt ?? (job.Status is FileJobStatus.Completed or FileJobStatus.Failed or FileJobStatus.ValidationFailed or FileJobStatus.Cancelled ? job.LastHeartbeatAt : now);
        return Math.Max(0, (end - startedAt).TotalSeconds);
    }

    private static string NormalizeStatus(FileJobStatus status)
    {
        return status switch
        {
            FileJobStatus.WaitingProcessing or FileJobStatus.ReadyToImport => "Aguardando",
            FileJobStatus.PreProcessing or FileJobStatus.Validating or FileJobStatus.Importing => "Processando",
            FileJobStatus.Completed => "Concluido",
            FileJobStatus.Cancelled => "Cancelado",
            _ => "Falhou"
        };
    }

    private static string StatusLabel(FileJobStatus status)
    {
        return status switch
        {
            FileJobStatus.WaitingProcessing => "Aguardando",
            FileJobStatus.PreProcessing => "Pre-processamento",
            FileJobStatus.Validating => "Validacao",
            FileJobStatus.ValidationFailed => "Validacao com erros",
            FileJobStatus.ReadyToImport => "Aguardando importacao",
            FileJobStatus.Importing => "Importacao",
            FileJobStatus.Completed => "Concluido",
            FileJobStatus.Cancelled => "Cancelado",
            _ => "Falhou"
        };
    }

    private static string StageName(string stage)
    {
        return stage switch
        {
            "UPLOAD" => "Upload",
            ImportProcessingStages.PreProcessing => "Pré-processamento",
            ImportProcessingStages.Validation => "Validação",
            ImportProcessingStages.Import => "Processamento",
            "SUMMARY" => "Resumo",
            "COMPLETED" => "Concluído",
            _ => stage
        };
    }

    private static string NormalizeStageCode(string? stage)
    {
        return string.IsNullOrWhiteSpace(stage) ? ImportProcessingStages.Validation : stage.Trim().ToUpperInvariant();
    }

    private static bool TryResolveManualAction(string action, out PostImportJobType jobType)
    {
        switch (action)
        {
            case "sales-summary":
                jobType = PostImportJobType.SalesSummary;
                return true;
            case "customer-summary":
                jobType = PostImportJobType.CustomerSummary;
                return true;
            default:
                jobType = default;
                return false;
        }
    }

    private ActionResult? EnsureAdminAccess()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        if (string.Equals(role, AppUserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Acesso negado",
            Detail = "A central de processamentos está disponível apenas para administradores.",
            Status = StatusCodes.Status403Forbidden
        });
    }
}
