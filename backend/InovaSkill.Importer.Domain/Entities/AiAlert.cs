namespace InovaSkill.Importer.Domain.Entities;

public sealed class AiAlert
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResponsibleArea { get; set; } = AiAlertAreas.Administrativo;
    public string ResponsibleManager { get; set; } = string.Empty;
    public string InvolvedAreasCsv { get; set; } = string.Empty;
    public string InvolvedUsersCsv { get; set; } = string.Empty;
    public string Severity { get; set; } = AiAlertSeverities.Medio;
    public string Status { get; set; } = AiAlertStatuses.Novo;
    public string Origin { get; set; } = AiAlertOrigins.Ia;
    public string EvidenceJson { get; set; } = "{}";
    public string ExpectedImpact { get; set; } = string.Empty;
    public DateTime ResponseDeadlineAt { get; set; }
    public DateTime? ActionDeadlineAt { get; set; }
    public string AiSuggestion { get; set; } = string.Empty;
    public bool RequiresMeeting { get; set; }
    public string RelatedTasksCsv { get; set; } = string.Empty;
    public string LinkedDecision { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public DateTime? ViewedAt { get; set; }
    public DateTime? LastNotificationAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public int NotificationCount { get; set; }
    public int EscalationCount { get; set; }

    public List<AiAlertStatusHistory> StatusHistory { get; set; } = [];
    public List<AiAlertNotificationHistory> NotificationHistory { get; set; } = [];
    public List<AiAlertEscalationHistory> EscalationHistory { get; set; } = [];

    public bool IsResolved => Status is AiAlertStatuses.Resolvido or AiAlertStatuses.CanceladoComJustificativa;
}

public sealed class AiAlertStatusHistory
{
    public long Id { get; set; }
    public long AiAlertId { get; set; }
    public AiAlert? AiAlert { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiAlertNotificationHistory
{
    public long Id { get; set; }
    public long AiAlertId { get; set; }
    public AiAlert? AiAlert { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Channel { get; set; } = AiAlertNotificationChannels.Sistema;
    public string Reason { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiAlertEscalationHistory
{
    public long Id { get; set; }
    public long AiAlertId { get; set; }
    public AiAlert? AiAlert { get; set; }
    public string FromRecipient { get; set; } = string.Empty;
    public string ToRecipient { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;
}

public static class AiAlertAreas
{
    public const string Vendas = "Vendas";
    public const string Logistica = "Logística";
    public const string Producao = "Produção";
    public const string Administrativo = "Administrativo";
    public const string Diretoria = "Diretoria";
}

public static class AiAlertSeverities
{
    public const string Baixo = "Baixo";
    public const string Medio = "Médio";
    public const string Alto = "Alto";
    public const string Critico = "Crítico";
}

public static class AiAlertStatuses
{
    public const string Novo = "Novo";
    public const string Visualizado = "Visualizado";
    public const string EmAnalise = "Em análise";
    public const string ReuniaoSugerida = "Reunião sugerida";
    public const string ReuniaoAgendada = "Reunião agendada";
    public const string DecisaoPendente = "Decisão pendente";
    public const string AcaoEmExecucao = "Ação em execução";
    public const string Atrasado = "Atrasado";
    public const string EscaladoParaDiretoria = "Escalado para diretoria";
    public const string Resolvido = "Resolvido";
    public const string CanceladoComJustificativa = "Cancelado com justificativa";
}

public static class AiAlertOrigins
{
    public const string Ia = "IA";
    public const string Manual = "Manual";
}

public static class AiAlertNotificationChannels
{
    public const string Sistema = "Sistema";
    public const string Email = "E-mail";
    public const string Painel = "Painel";
}
