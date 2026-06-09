namespace InovaSkill.Importer.Application.Events;

public static class ProcessingEventTypes
{
    public const string JobRequested = "JobRequestedEvent";
    public const string FileUploaded = "FileUploadedEvent";
    public const string ImportRequested = "ImportRequestedEvent";
    public const string SummaryGenerationRequested = "SummaryGenerationRequestedEvent";
    public const string AnalyticsRefreshRequested = "AnalyticsRefreshRequestedEvent";
    public const string JobCompleted = "JobCompletedEvent";
    public const string JobFailed = "JobFailedEvent";
}
