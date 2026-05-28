namespace InovaSkill.Importer.Domain.Enums;

public enum FileJobStatus
{
    WaitingProcessing = 0,
    PreProcessing = 1,
    Validating = 2,
    ValidationFailed = 3,
    ReadyToImport = 4,
    Importing = 5,
    Completed = 6,
    Failed = 7
}
