namespace InovaSkill.Importer.Api.Contracts;

public sealed record AiAlertDashboardDto(
    AiAlertSummaryDto Summary,
    IReadOnlyList<AiAlertItemDto> Alerts);

public sealed record AiAlertSummaryDto(
    int Total,
    int Critical,
    int Late,
    int Escalated,
    int RequiresMeeting,
    IReadOnlyList<AiAlertAreaSummaryDto> ByArea);

public sealed record AiAlertAreaSummaryDto(
    string Area,
    int Total,
    int Critical,
    int Late);

public sealed record AiAlertItemDto(
    long Id,
    string Title,
    string Description,
    string ResponsibleArea,
    string ResponsibleManager,
    IReadOnlyList<string> InvolvedAreas,
    IReadOnlyList<string> InvolvedUsers,
    string Severity,
    string Status,
    string Origin,
    string EvidenceJson,
    string ExpectedImpact,
    DateTime ResponseDeadlineAt,
    DateTime? ActionDeadlineAt,
    string AiSuggestion,
    bool RequiresMeeting,
    IReadOnlyList<string> RelatedTasks,
    string LinkedDecision,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string CancellationReason,
    DateTime? ViewedAt,
    DateTime? LastNotificationAt,
    DateTime? EscalatedAt,
    int NotificationCount,
    int EscalationCount,
    bool IsLate,
    IReadOnlyList<AiAlertStatusHistoryDto> StatusHistory,
    IReadOnlyList<AiAlertNotificationHistoryDto> NotificationHistory,
    IReadOnlyList<AiAlertEscalationHistoryDto> EscalationHistory);

public sealed record AiAlertStatusHistoryDto(
    string PreviousStatus,
    string NewStatus,
    string ChangedBy,
    string Justification,
    DateTime ChangedAt);

public sealed record AiAlertNotificationHistoryDto(
    string Recipient,
    string Channel,
    string Reason,
    DateTime SentAt);

public sealed record AiAlertEscalationHistoryDto(
    string FromRecipient,
    string ToRecipient,
    string Reason,
    DateTime EscalatedAt);

public sealed record CreateAiAlertRequestDto(
    string Title,
    string Description,
    string ResponsibleArea,
    string ResponsibleManager,
    IReadOnlyList<string>? InvolvedAreas,
    IReadOnlyList<string>? InvolvedUsers,
    string Severity,
    string? Origin,
    string? EvidenceJson,
    string ExpectedImpact,
    DateTime ResponseDeadlineAt,
    DateTime? ActionDeadlineAt,
    string AiSuggestion,
    bool RequiresMeeting,
    IReadOnlyList<string>? RelatedTasks,
    string? LinkedDecision);

public sealed record UpdateAiAlertStatusRequestDto(
    string Status,
    string? Justification,
    string? CancellationReason);

public sealed record EvaluateAiAlertEscalationResponseDto(
    long Id,
    string Status,
    int NotificationCount,
    int EscalationCount,
    bool Escalated,
    string Message);
