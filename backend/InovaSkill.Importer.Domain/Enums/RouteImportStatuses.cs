namespace InovaSkill.Importer.Domain.Enums;

public enum RouteImportStatus
{
    Queued,
    Processing,
    NeedsReview,
    Completed,
    Failed
}

public enum ImportErrorStatus
{
    Pending,
    Resolved
}

public enum JobExecutionStatus
{
    Queued,
    Processing,
    Retrying,
    Completed,
    Failed
}
