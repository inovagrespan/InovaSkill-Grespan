using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class RedisProcessingEventQueue(IConnectionMultiplexer multiplexer, IConfiguration configuration) : IProcessingEventQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = multiplexer.GetDatabase();
    private readonly string _queueKey = configuration["ImportProcessing:EventQueueKey"] ?? "import:events";
    private readonly string _deadLetterKey = configuration["ImportProcessing:DeadLetterQueueKey"] ?? "import:events:dead";

    public async Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ListRightPushAsync(_queueKey, JsonSerializer.Serialize(envelope, JsonOptions));
    }

    public async Task<ProcessingEventEnvelope?> DequeueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.ListLeftPopAsync(_queueKey);
        return value.HasValue
            ? JsonSerializer.Deserialize<ProcessingEventEnvelope>(value.ToString(), JsonOptions)
            : null;
    }

    public async Task PublishDeadLetterAsync(ProcessingEventEnvelope envelope, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(new { envelope, reason, failedAt = DateTime.UtcNow }, JsonOptions);
        await _database.ListRightPushAsync(_deadLetterKey, payload);
    }
}
