namespace InovaSkill.Importer.Domain.Entities;

public sealed class Meeting
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = MeetingStatus.Draft;
    public string CurrentStage { get; set; } = MeetingStage.Context;
    public long CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ConcludedAt { get; set; }
    public string Context { get; set; } = string.Empty;
    public string InvolvedAreasCsv { get; set; } = string.Empty;
    public string AiSummary { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;

    public List<MeetingParticipant> Participants { get; set; } = [];
    public List<MeetingComment> Comments { get; set; } = [];
    public List<MeetingProblem> Problems { get; set; } = [];
    public List<MeetingQuestion> Questions { get; set; } = [];
    public List<MeetingAiAnalysis> AiAnalyses { get; set; } = [];
    public List<MeetingDecision> Decisions { get; set; } = [];
    public List<MeetingAction> Actions { get; set; } = [];
    public List<MeetingHistory> History { get; set; } = [];
}

public static class MeetingStatus
{
    public const string Draft = "rascunho";
    public const string InProgress = "em_andamento";
    public const string AwaitingAnswers = "aguardando_respostas";
    public const string InAiAnalysis = "em_analise_ia";
    public const string AwaitingConclusion = "aguardando_conclusao";
    public const string Concluded = "concluida";
    public const string Cancelled = "cancelada";
}

public static class MeetingStage
{
    public const string Context = "contexto";
    public const string Discussion = "discussao";
    public const string Problems = "problemas";
    public const string QuestionsAndAnswers = "perguntas_e_respostas";
    public const string Solutions = QuestionsAndAnswers;
    public const string AiAnalysis = "analise_ia";
    public const string Conclusion = "conclusao";
    public const string Actions = "acoes";
    public const string FollowUp = "acompanhamento";
}
