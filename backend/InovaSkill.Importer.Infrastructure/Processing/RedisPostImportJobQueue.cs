using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class RedisPostImportJobQueue(IConnectionMultiplexer multiplexer, IConfiguration configuration) : IPostImportJobQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = multiplexer.GetDatabase();
    private readonly string _queueKey = configuration["ImportProcessing:PostImportQueueKey"] ?? "import:post-jobs";

    public async Task EnqueueAsync(PostImportJobItem job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(job, JsonOptions);
        await _database.ListRightPushAsync(_queueKey, payload);
    }

    public async Task<PostImportJobItem?> DequeueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.ListLeftPopAsync(_queueKey);
        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<PostImportJobItem>(value.ToString(), JsonOptions);
    }
}
