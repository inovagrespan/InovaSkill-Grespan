using InovaSkill.Importer.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class RedisFileJobQueue(IConnectionMultiplexer multiplexer, IConfiguration configuration) : IFileJobQueue
{
    private readonly IDatabase _database = multiplexer.GetDatabase();
    private readonly string _queueKey = configuration["ImportProcessing:QueueKey"] ?? "import:file-jobs";

    public async Task EnqueueAsync(long fileJobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ListRightPushAsync(_queueKey, fileJobId.ToString());
    }

    public async Task<long?> DequeueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.ListLeftPopAsync(_queueKey);
        if (!value.HasValue)
        {
            return null;
        }

        return long.TryParse(value.ToString(), out var fileJobId) ? fileJobId : null;
    }
}
