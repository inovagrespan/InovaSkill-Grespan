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
    Failed,
    Cancelled
}

public enum JobExecutionTrigger
{
    Manual,
    Schedule,
    Import,
    Webhook,
    Retry,
    System
}

public enum DataSourceImportMode
{
    Snapshot,
    Append,
    Upsert
}

public enum FiscalMovementCategory
{
    Unknown,
    Sale,
    Return,
    Bonus,
    Loan,
    Exchange
}

public enum RouteOccupancyStatus
{
    Calculated,
    MissingCapacity
}
