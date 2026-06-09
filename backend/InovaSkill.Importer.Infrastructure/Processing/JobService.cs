using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class JobService(
    ImportDbContext dbContext,
    IEnumerable<IJobPayloadValidator> validators,
    IEnumerable<IJobHandler> handlers,
    IProcessingEventPublisher eventPublisher) : IJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<long> EnqueueAsync(
        string type,
        object payload,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("O tipo do Job e obrigatorio.");
        }

        var normalizedType = type.Trim();
        if (!handlers.Any(x => string.Equals(x.JobType, normalizedType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"No job handler registered for {normalizedType}.");
        }

        var payloadElement = JsonSerializer.SerializeToElement(payload, JsonOptions);

        foreach (var validator in validators.Where(x => string.Equals(x.JobType, normalizedType, StringComparison.OrdinalIgnoreCase)))
        {
            await validator.ValidateAsync(payloadElement, cancellationToken);
        }

        var job = new Job
        {
            Type = normalizedType,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            PayloadJson = payloadElement.GetRawText(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        job.MarkQueued();
        await dbContext.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishAsync(
            ProcessingEventEnvelope.Create(ProcessingEventTypes.JobRequested, job.Id, payloadElement),
            cancellationToken);

        return job.Id;
    }
}
