namespace InovaSkill.Importer.Domain.Entities;

public sealed class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = NotificationType.General;
    public string Priority { get; set; } = NotificationPriority.Medium;
    public string Status { get; set; } = NotificationStatus.Unread;
    public string RelatedLink { get; set; } = string.Empty;
    public string RelatedEntity { get; set; } = string.Empty;
    public long? RelatedEntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public static class NotificationType
{
    public const string General = "geral";
    public const string MeetingInvite = "convite_reuniao";
    public const string MeetingStageChange = "mudanca_etapa_reuniao";
    public const string QuestionPending = "pergunta_pendente";
    public const string AnswerPending = "resposta_pendente";
    public const string ActionAssigned = "acao_atribuida";
    public const string ActionDueSoon = "acao_proxima_do_prazo";
    public const string ActionOverdue = "acao_atrasada";
    public const string CriticalPendingAlert = "pendencia_critica";
    public const string PendingEscalated = "pendencia_escalada";
    public const string DecisionRegistered = "decisao_registrada";
    public const string MeetingConcluded = "reuniao_concluida";
    public const string DirectorEscalation = "escalonamento_diretor";
}

public static class NotificationPriority
{
    public const string Low = "baixa";
    public const string Medium = "media";
    public const string High = "alta";
    public const string Critical = "critica";
}

public static class NotificationStatus
{
    public const string Unread = "nao_lida";
    public const string Read = "lida";
    public const string Archived = "arquivada";
}
