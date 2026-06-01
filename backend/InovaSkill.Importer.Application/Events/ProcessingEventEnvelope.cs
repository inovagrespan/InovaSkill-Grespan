using System.Text.Json;

namespace InovaSkill.Importer.Application.Events;

public sealed record ProcessingEventEnvelope(
    string EventType,
    long JobId,
    string? FileId,
    string? CompanyId,
    string? UserId,
    Guid CorrelationId,
    DateTime CreatedAt,
    int RetryCount,
    JsonElement? Payload)
{
    public static ProcessingEventEnvelope Create(
        string eventType,
        long jobId,
        JsonElement? payload = null,
        Guid? correlationId = null,
        int retryCount = 0)
    {
        return new ProcessingEventEnvelope(
            eventType,
            jobId,
            FileId: jobId.ToString(),
            CompanyId: null,
            UserId: null,
            correlationId ?? Guid.NewGuid(),
            DateTime.UtcNow,
            retryCount,
            payload);
    }
}
