namespace InovaSkill.Importer.Domain.Entities;

public sealed class CriticalPending
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Origin { get; set; } = PendingOrigin.AiDetection;
    public string Sector { get; set; } = string.Empty;
    public long? ResponsibleUserId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string Priority { get; set; } = PendingPriority.Medium;
    public string Status { get; set; } = PendingStatus.New;
    public int DeadlineDays { get; set; }
    public long? SourceMeetingId { get; set; }
    public long? RelatedActionId { get; set; }
    public long? RelatedDecisionId { get; set; }
    public string NotificationHistoryJson { get; set; } = string.Empty;
    public string EscalationHistoryJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }
    public string AiSuggestion { get; set; } = string.Empty;
}

public static class PendingOrigin
{
    public const string PreviousMeeting = "reuniao_anterior";
    public const string OverdueAction = "acao_atrasada";
    public const string UnresolvedProblem = "problema_nao_resolvido";
    public const string DirectorManual = "cadastro_manual";
    public const string AiDetection = "risco_ia";
    public const string NoResponsible = "sem_responsavel";
    public const string DecisionNotExecuted = "decisao_nao_executada";
    public const string RecurringProblem = "problema_recorrente";
    public const string IncompleteItem = "item_nao_concluido";
    public const string UnansweredQuestion = "pergunta_sem_resposta";
}

public static class PendingStatus
{
    public const string New = "nova";
    public const string InAnalysis = "em_analise";
    public const string Assigned = "atribuida";
    public const string InExecution = "em_execucao";
    public const string Overdue = "atrasada";
    public const string Escalated = "escalada";
    public const string Resolved = "resolvida";
    public const string CancelledWithJustification = "cancelada_com_justificativa";
}

public static class PendingPriority
{
    public const string Low = "baixa";
    public const string Medium = "media";
    public const string High = "alta";
    public const string Critical = "critica";
}
