namespace InovaSkill.Importer.Application.Detection;

public static class DetectorCodes
{
    public const string CustomerPurchaseDrop = "CUSTOMER_PURCHASE_DROP";
    public const string RouteOccupancyAnomaly = "ROUTE_OCCUPANCY_ANOMALY";
}

public sealed record DetectionContext(
    Guid DetectionRunId,
    Guid DetectorDefinitionId,
    string DetectorCode,
    DateTime ReferenceTime);

public sealed record DetectionResult(
    int AnalyzedItems,
    IReadOnlyCollection<FindingCandidate> Findings)
{
    public int FindingsCount => Findings.Count;
}

public sealed record FindingCandidate(
    string Fingerprint,
    string Title,
    string Description,
    string SubjectType,
    string SubjectId,
    string? SubjectLabel,
    IReadOnlyCollection<FindingEvidenceCandidate> Evidences);

public sealed record FindingEvidenceCandidate(
    string Name,
    string Value,
    string? ReferenceValue,
    string? Unit,
    string? Description,
    string? SourceType,
    string? SourceId,
    DateTime ObservedAt);

public interface IDetector
{
    string Code { get; }

    Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken);
}

public interface IDetectorRegistry
{
    IDetector Get(string code);
}

public interface IDetectionJobDispatcher
{
    string Enqueue(Guid detectionRunId);
}
