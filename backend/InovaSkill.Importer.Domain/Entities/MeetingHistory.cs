namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingHistory
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DataBefore { get; set; } = string.Empty;
    public string DataAfter { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
}

public static class MeetingHistoryEvent
{
    public const string Created = "meeting.created";
    public const string Started = "meeting.started";
    public const string StageChanged = "meeting.stage_changed";
    public const string CommentAdded = "meeting.comment_added";
    public const string ProblemCreated = "meeting.problem_created";
    public const string ProblemApproved = "meeting.problem_approved";
    public const string QuestionsGenerated = "meeting.questions_generated";
    public const string QuestionCreated = "meeting.question_created";
    public const string AnswerSubmitted = "meeting.answer_submitted";
    public const string AiAnalysisGenerated = "meeting.ai_analysis_generated";
    public const string DecisionCreated = "meeting.decision_created";
    public const string ActionCreated = "meeting.action_created";
    public const string PendingAdded = "meeting.pending_added";
    public const string Completed = "meeting.completed";
    public const string Cancelled = "meeting.cancelled";
}
