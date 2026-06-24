using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record MeetingListDto(
    long Id,
    string Title,
    string Description,
    string Status,
    string CurrentStage,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    int ParticipantCount,
    int ProblemCount,
    int QuestionCount,
    int OverdueActionCount);

public sealed record MeetingDetailDto(
    long Id,
    string Title,
    string Description,
    string Reason,
    string Status,
    string CurrentStage,
    long CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    DateTime? ConcludedAt,
    string Context,
    string InvolvedAreasCsv,
    string AiSummary,
    string CancellationReason,
    IReadOnlyList<MeetingParticipantDto> Participants,
    IReadOnlyList<MeetingCommentDto> Comments,
    IReadOnlyList<MeetingProblemDto> Problems,
    IReadOnlyList<MeetingQuestionDto> Questions,
    IReadOnlyList<MeetingAiAnalysisDto> AiAnalyses,
    IReadOnlyList<MeetingDecisionDto> Decisions,
    IReadOnlyList<MeetingActionDto> Actions,
    IReadOnlyList<MeetingHistoryDto> History,
    IReadOnlyList<CriticalPendingSummaryDto> RelatedPendencies);

public sealed record MeetingParticipantDto(
    long Id,
    long UserId,
    string UserName,
    string UserEmail,
    string UserRole,
    string UserSector,
    string RoleInMeeting,
    string ParticipationStatus,
    DateTime InvitedAt);

public sealed record MeetingCommentDto(
    long Id,
    long UserId,
    string UserName,
    string Message,
    string Stage,
    bool IsImportant,
    DateTime CreatedAt);

public sealed record MeetingProblemDto(
    long Id,
    string Sector,
    string Description,
    string Severity,
    string Origin,
    long CreatedByUserId,
    string CreatedByName,
    bool ApprovedByDirector,
    string AiSuggestion,
    DateTime CreatedAt,
    IReadOnlyList<MeetingQuestionDto> Questions);

public sealed record MeetingQuestionDto(
    long Id,
    long ProblemId,
    string Question,
    long ResponsibleUserId,
    string ResponsibleName,
    string Sector,
    bool IsRequired,
    string Status,
    DateTime? AnswerDeadline,
    DateTime CreatedAt,
    MeetingAnswerDto? Answer);

public sealed record MeetingAnswerDto(
    long Id,
    long UserId,
    string UserName,
    string Sector,
    string Answer,
    DateTime CreatedAt);

public sealed record MeetingAiAnalysisDto(
    long Id,
    long ProblemId,
    string ProblemDescription,
    string ProposedSolution,
    bool MakesSense,
    string PositivePoints,
    string NegativePoints,
    string Risks,
    string ExpectedImpact,
    string Recommendation,
    string AlternativeSolution,
    string SuggestedDecision,
    string RelatedPendencies,
    DateTime CreatedAt);

public sealed record MeetingHistoryDto(
    long Id,
    string EventType,
    string Description,
    long UserId,
    string UserName,
    string DataBefore,
    string DataAfter,
    DateTime CreatedAt);

public sealed record MeetingDecisionDto(
    long Id,
    long ProblemId,
    string ProblemDescription,
    string ChosenSolution,
    string SolutionOrigin,
    string Justification,
    long ResponsibleUserId,
    string ResponsibleName,
    string Sector,
    int DeadlineDays,
    string Priority,
    string TrackingMetric,
    string AcceptedRisk,
    string NextSteps,
    string ClosedPendencies,
    DateTime CreatedAt);

public sealed record MeetingActionDto(
    long Id,
    long? DecisionId,
    string Title,
    string Description,
    long ResponsibleUserId,
    string ResponsibleName,
    string Sector,
    int DeadlineDays,
    string Priority,
    string Status,
    string CompletionEvidence,
    string Comments,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? DeadlineAt);

public sealed record CriticalPendingSummaryDto(
    long Id,
    string Title,
    string Description,
    string Origin,
    string Sector,
    string ResponsibleName,
    string Priority,
    string Status,
    int DeadlineDays,
    DateTime? DeadlineAt,
    DateTime CreatedAt);

public sealed record CriticalPendingDetailDto(
    long Id,
    string Title,
    string Description,
    string Origin,
    string Sector,
    long? ResponsibleUserId,
    string ResponsibleName,
    string Priority,
    string Status,
    int DeadlineDays,
    long? SourceMeetingId,
    long? RelatedActionId,
    long? RelatedDecisionId,
    string NotificationHistoryJson,
    string EscalationHistoryJson,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    DateTime? DeadlineAt,
    string AiSuggestion);

public sealed record CreateMeetingRequestDto(
    string Title,
    string Description,
    string Reason,
    IReadOnlyList<long> ParticipantUserIds,
    DateTime? ScheduledAt,
    string Context,
    string InvolvedAreasCsv,
    IReadOnlyList<string> InitialProblems);

public sealed record AddCommentRequestDto(
    string Message,
    string Stage);

public sealed record AddProblemRequestDto(
    string Sector,
    string Description,
    string Severity,
    string Origin);

public sealed record AddQuestionRequestDto(
    long ProblemId,
    string Question,
    long ResponsibleUserId,
    string Sector,
    bool IsRequired);

public sealed record AnswerQuestionRequestDto(
    string Answer);

public sealed record CreateDecisionRequestDto(
    long ProblemId,
    string ChosenSolution,
    string SolutionOrigin,
    string Justification,
    long ResponsibleUserId,
    int DeadlineDays,
    string Priority,
    string TrackingMetric,
    string AcceptedRisk,
    string NextSteps,
    string ClosedPendencies);

public sealed record CreateActionRequestDto(
    long? DecisionId,
    string Title,
    string Description,
    long ResponsibleUserId,
    string Sector,
    int DeadlineDays,
    string Priority);

public sealed record UpdateActionStatusRequestDto(
    string Status,
    string CompletionEvidence,
    string Comments);

public sealed record UpdateMeetingStageRequestDto(
    string Stage,
    bool Force = false,
    string Justification = "");

public sealed record GenerateMeetingAiAnalysisRequestDto(
    bool Force = false,
    string Justification = "");

public sealed record CancelMeetingRequestDto(
    string Reason);

public sealed record AddPendingToMeetingRequestDto(
    long PendingId);

public sealed record IgnorePendingRequestDto(
    long PendingId,
    string Justification);

public sealed record CreateCriticalPendingRequestDto(
    string Title,
    string Description,
    string Origin,
    string Sector,
    long? ResponsibleUserId,
    string Priority,
    int DeadlineDays,
    long? SourceMeetingId,
    long? RelatedActionId,
    long? RelatedDecisionId);

public sealed record UpdatePendingStatusRequestDto(
    string Status,
    string Justification);

public sealed record PreMeetingBriefingDto(
    int TotalPendencies,
    IReadOnlyList<CriticalPendingSummaryDto> Pendencies,
    string AiSummary);

public sealed record NotificationDto(
    long Id,
    long UserId,
    string Title,
    string Message,
    string Type,
    string Priority,
    string Status,
    string RelatedLink,
    string RelatedEntity,
    long? RelatedEntityId,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationListDto(
    int Total,
    int UnreadCount,
    IReadOnlyList<NotificationDto> Notifications);

public sealed record UnreadCountDto(
    int Count);

public sealed record UserListItemDto(
    long Id,
    string Name,
    string Email,
    string Role,
    string Sector);
