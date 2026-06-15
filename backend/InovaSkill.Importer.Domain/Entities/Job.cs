using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class Job
{
    private const int MinimumProgressPercent = 0;
    private const int MaximumProgressPercent = 100;
    private const int MaximumErrorLength = 4000;
    private const int MaximumStepLength = 128;

    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int ProgressPercent { get; set; }
    public string CurrentStep { get; set; } = "Aguardando processamento";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? UserId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string Error { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string LockedBy { get; set; } = string.Empty;
    public DateTime? LockedAt { get; set; }

    public void MarkQueued()
    {
        EnsureStatus(JobStatus.Pending, JobStatus.Failed, JobStatus.Cancelled);
        Status = JobStatus.Queued;
        CurrentStep = "Enfileirado";
        FinishedAt = null;
        Error = string.Empty;
    }

    public void MarkProcessing(string? step = null)
    {
        EnsureStatus(JobStatus.Queued, JobStatus.Processing);
        Status = JobStatus.Processing;
        StartedAt ??= DateTime.UtcNow;
        FinishedAt = null;
        CurrentStep = TruncateStep(string.IsNullOrWhiteSpace(step) ? "Processando" : step);
    }

    public void UpdateProgress(string step, int progressPercent)
    {
        CurrentStep = TruncateStep(step);
        ProgressPercent = Math.Clamp(progressPercent, MinimumProgressPercent, MaximumProgressPercent);
    }

    public void MarkCompleted(string? resultJson = null)
    {
        EnsureStatus(JobStatus.Processing, JobStatus.Queued);
        Status = JobStatus.Completed;
        ProgressPercent = MaximumProgressPercent;
        CurrentStep = "Processamento concluido";
        FinishedAt = DateTime.UtcNow;
        ResultJson = resultJson;
        Error = string.Empty;
    }

    public void ScheduleRetry(string reason)
    {
        RetryCount++;
        Status = JobStatus.Queued;
        CurrentStep = "Nova tentativa agendada";
        FinishedAt = null;
        Error = TruncateError(reason);
    }

    public void MarkFailed(string reason)
    {
        Status = JobStatus.Failed;
        CurrentStep = "Falha no processamento";
        FinishedAt = DateTime.UtcNow;
        Error = TruncateError(reason);
    }

    public void MarkCancelled(string? reason = null)
    {
        Status = JobStatus.Cancelled;
        CurrentStep = string.IsNullOrWhiteSpace(reason) ? "Processamento cancelado" : TruncateStep(reason);
        FinishedAt = DateTime.UtcNow;
    }

    private void EnsureStatus(params JobStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot move job from status {Status}.");
        }
    }

    private static string TruncateStep(string value)
    {
        return value.Length <= MaximumStepLength ? value : value[..MaximumStepLength];
    }

    private static string TruncateError(string value)
    {
        return value.Length <= MaximumErrorLength ? value : value[..MaximumErrorLength];
    }
}
