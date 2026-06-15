namespace InovaSkill.Importer.Api.Contracts;

public sealed record ProcessingMonitoringDashboardDto(
    ProcessingMonitoringSummaryDto Summary,
    IReadOnlyList<ProcessingJobQueueItemDto> Jobs,
    IReadOnlyList<ProcessingDailyPointDto> Daily,
    IReadOnlyList<ProcessingStageDurationDto> StageDurations,
    IReadOnlyList<WorkerHealthDto> Workers);

public sealed record ProcessingMonitoringSummaryDto(
    int RunningJobs,
    int QueuedJobs,
    int CompletedToday,
    int FailedJobs,
    double AverageProcessingSeconds,
    int ProcessedRowsToday,
    int StaleJobs);

public sealed record ProcessingJobQueueItemDto(
    long Id,
    string Company,
    string FileName,
    string? Template,
    string Status,
    string StatusLabel,
    string CurrentStep,
    int ProgressPercent,
    string? CurrentStageCode,
    string? CurrentStageName,
    IReadOnlyList<FileJobStageProgressDto> Stages,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    double ElapsedSeconds,
    int ProcessedRows,
    int TotalRows,
    int ErrorCount);

public sealed record ProcessingDailyPointDto(
    DateTime Date,
    int Jobs,
    int CompletedJobs,
    int FailedJobs,
    int ProcessedRows,
    double AverageProcessingSeconds,
    double SuccessRatePercent);

public sealed record ProcessingStageDurationDto(
    string Stage,
    string StageName,
    double AverageDurationSeconds,
    double SharePercent);

public sealed record WorkerHealthDto(
    string WorkerId,
    string Status,
    DateTime LastSeenAt,
    double SecondsSinceLastSeen,
    int ProcessedJobsToday,
    double IdleSeconds,
    long? CurrentJobId,
    string CurrentTask);

public sealed record ProcessingJobDetailsDto(
    ProcessingJobQueueItemDto Job,
    IReadOnlyList<ProcessingStepDto> Timeline,
    ProcessingJobMetricsDto Metrics,
    IReadOnlyList<ProcessingStageDurationDto> PerformanceByStage,
    IReadOnlyList<ProcessingLogDto> Logs);

public sealed record ProcessingStepDto(
    string Step,
    string StepName,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    double DurationSeconds,
    string Status,
    int ProcessedRows,
    int ErrorCount);

public sealed record ProcessingJobMetricsDto(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int ImportedRows,
    int ErrorCount,
    int WarningCount);

public sealed record ProcessingLogDto(
    DateTime Timestamp,
    long FileJobId,
    string Stage,
    string Level,
    string Message);
