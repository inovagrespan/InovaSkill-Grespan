using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Api.Presentation;

internal static class FileJobStageProgressPresenter
{
    public static IReadOnlyList<FileJobStageProgressDto> Build(
        FileJob job,
        IReadOnlyDictionary<(long FileJobId, string Stage), int> errorCountLookup)
    {
        return ImportProcessingStages.All
            .Select(stage => new FileJobStageProgressDto(
                stage.Code,
                stage.Name,
                ResolveStageStatus(job, stage.Code, GetStageErrorCount(job.Id, stage.Code, errorCountLookup)),
                ResolveStageProgress(job, stage.Code),
                GetStageErrorCount(job.Id, stage.Code, errorCountLookup)))
            .ToList();
    }

    public static (string? Code, string? Name) ResolveCurrentStage(IReadOnlyList<FileJobStageProgressDto> stages)
    {
        var currentStage = stages.FirstOrDefault(stage => stage.Status == StageProgressStatus.Running);
        return (currentStage?.Code, currentStage?.Name);
    }

    private static int GetStageErrorCount(
        long fileJobId,
        string stage,
        IReadOnlyDictionary<(long FileJobId, string Stage), int> errorCountLookup)
    {
        return errorCountLookup.TryGetValue((fileJobId, stage), out var count) ? count : 0;
    }

    private static string ResolveStageStatus(FileJob job, string stage, int errorCount)
    {
        if (job.Status == FileJobStatus.Failed && IsCurrentFailureStage(job, stage))
        {
            return StageProgressStatus.Failed;
        }

        return stage switch
        {
            ImportProcessingStages.PreProcessing => ResolvePreProcessingStatus(job, errorCount),
            ImportProcessingStages.Validation => ResolveValidationStatus(job, errorCount),
            ImportProcessingStages.Import => ResolveImportStatus(job),
            _ => StageProgressStatus.Pending
        };
    }

    private static string ResolvePreProcessingStatus(FileJob job, int errorCount)
    {
        if (job.Status == FileJobStatus.PreProcessing)
        {
            return StageProgressStatus.Running;
        }

        if (job.Status == FileJobStatus.ValidationFailed && errorCount > 0)
        {
            return StageProgressStatus.Failed;
        }

        return job.Status is FileJobStatus.Validating
            or FileJobStatus.ValidationFailed
            or FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed
            ? StageProgressStatus.Completed
            : StageProgressStatus.Pending;
    }

    private static string ResolveValidationStatus(FileJob job, int errorCount)
    {
        if (job.Status == FileJobStatus.Validating)
        {
            return StageProgressStatus.Running;
        }

        if (job.Status == FileJobStatus.ValidationFailed && errorCount > 0)
        {
            return StageProgressStatus.Failed;
        }

        return job.Status is FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed
            ? StageProgressStatus.Completed
            : StageProgressStatus.Pending;
    }

    private static string ResolveImportStatus(FileJob job)
    {
        return job.Status switch
        {
            FileJobStatus.Importing => StageProgressStatus.Running,
            FileJobStatus.Completed => StageProgressStatus.Completed,
            FileJobStatus.Failed when job.CurrentStep.Contains("import", StringComparison.OrdinalIgnoreCase) => StageProgressStatus.Failed,
            _ => StageProgressStatus.Pending
        };
    }

    private static int ResolveStageProgress(FileJob job, string stage)
    {
        return stage switch
        {
            ImportProcessingStages.PreProcessing when job.Status == FileJobStatus.PreProcessing => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.Validation when job.Status == FileJobStatus.Validating => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.Import when job.Status == FileJobStatus.Importing => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.PreProcessing when IsPreProcessingComplete(job.Status) => StageProgressPercent.Complete,
            ImportProcessingStages.Validation when IsValidationComplete(job.Status) => StageProgressPercent.Complete,
            ImportProcessingStages.Import when job.Status == FileJobStatus.Completed => StageProgressPercent.Complete,
            _ => StageProgressPercent.Pending
        };
    }

    private static bool IsPreProcessingComplete(FileJobStatus status)
    {
        return status is FileJobStatus.Validating
            or FileJobStatus.ValidationFailed
            or FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed;
    }

    private static bool IsValidationComplete(FileJobStatus status)
    {
        return status is FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed;
    }

    private static bool IsCurrentFailureStage(FileJob job, string stage)
    {
        var step = job.CurrentStep;

        return stage switch
        {
            ImportProcessingStages.Import => step.Contains("import", StringComparison.OrdinalIgnoreCase),
            ImportProcessingStages.Validation => step.Contains("valid", StringComparison.OrdinalIgnoreCase),
            ImportProcessingStages.PreProcessing => step.Contains("pre", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static int ClampProgress(int value)
    {
        return Math.Clamp(value, StageProgressPercent.Pending, StageProgressPercent.Complete);
    }

    private static class StageProgressStatus
    {
        public const string Pending = "pending";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    private static class StageProgressPercent
    {
        public const int Pending = 0;
        public const int Complete = 100;
    }
}
