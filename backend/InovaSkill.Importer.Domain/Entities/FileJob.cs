using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class FileJob
{
    public long Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string NormalizedFilePath { get; set; } = string.Empty;
    public string? ImportFileTypeCode { get; set; }
    public FileJobStatus Status { get; set; } = FileJobStatus.WaitingProcessing;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CurrentStep { get; set; } = "Aguardando validacao";
    public int ProgressPercent { get; set; }
    public int ProcessedRows { get; set; }
    public int TotalRows { get; set; }
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;

    public FileJobStatus StartNextStage()
    {
        if (Status == FileJobStatus.WaitingProcessing)
        {
            Status = FileJobStatus.PreProcessing;
            CurrentStep = "Pre-processando arquivo";
            ProgressPercent = 0;
            ProcessedRows = 0;
            TotalRows = 0;
            TouchHeartbeat();
            return Status;
        }

        if (Status == FileJobStatus.ReadyToImport)
        {
            Status = FileJobStatus.Importing;
            CurrentStep = "Importando dados";
            ProgressPercent = 0;
            ProcessedRows = 0;
            TouchHeartbeat();
            return Status;
        }

        throw new InvalidOperationException($"Cannot start next stage from status {Status}.");
    }

    public void MarkFailed(string? reason = null)
    {
        Status = FileJobStatus.Failed;
        CurrentStep = string.IsNullOrWhiteSpace(reason)
            ? "Falha no processamento"
            : Truncate($"Falha: {reason}", 128);
        TouchHeartbeat();
    }

    public void MarkValidating()
    {
        Status = FileJobStatus.Validating;
        CurrentStep = "Validando arquivo normalizado";
        ProgressPercent = 0;
        ProcessedRows = 0;
        TouchHeartbeat();
    }

    public void MarkValidationFailed()
    {
        Status = FileJobStatus.ValidationFailed;
        CurrentStep = "Validacao com erros";
        ProgressPercent = 100;
        TouchHeartbeat();
    }

    public void MarkReadyToImport()
    {
        Status = FileJobStatus.ReadyToImport;
        CurrentStep = "Pronto para importar";
        ProgressPercent = 100;
        TouchHeartbeat();
    }

    public void MarkCompleted(int finalProcessedRows)
    {
        Status = FileJobStatus.Completed;
        CurrentStep = "Processamento concluido";
        ProgressPercent = 100;
        ProcessedRows = finalProcessedRows;
        TouchHeartbeat();
    }

    public void RequeueManually()
    {
        Status = FileJobStatus.WaitingProcessing;
        CurrentStep = "Reenfileirado manualmente";
        ProgressPercent = 0;
        ProcessedRows = 0;
        TotalRows = 0;
        TouchHeartbeat();
    }

    public void RecoverAfterStaleProcessing()
    {
        Status = FileJobStatus.ReadyToImport;
        CurrentStep = "Retomado apos falha. Pronto para importar novamente";
        ProgressPercent = 0;
        ProcessedRows = 0;
        TouchHeartbeat();
    }

    public void RecoverAfterStaleValidation()
    {
        Status = FileJobStatus.WaitingProcessing;
        CurrentStep = "Retomado apos falha. Aguardando validacao";
        ProgressPercent = 0;
        ProcessedRows = 0;
        TotalRows = 0;
        TouchHeartbeat();
    }

    public void UpdateProgress(string step, int progressPercent, int processedRows)
    {
        CurrentStep = step;
        ProgressPercent = progressPercent;
        ProcessedRows = processedRows;
        TouchHeartbeat();
    }

    public void TouchHeartbeat()
    {
        LastHeartbeatAt = DateTime.UtcNow;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}


