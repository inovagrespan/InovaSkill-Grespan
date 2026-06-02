using InovaSkill.Importer.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class RedisProcessingQueueMonitor(IConnectionMultiplexer multiplexer, IConfiguration configuration) : IProcessingQueueMonitor
{
    private readonly IDatabase _database = multiplexer.GetDatabase();
    private readonly string _importQueueKey = configuration["ImportProcessing:EventQueueKey"] ?? "import:events";
    private readonly string _postImportQueueKey = configuration["ImportProcessing:DeadLetterQueueKey"] ?? "import:events:dead";

    public async Task<ProcessingQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var importQueueLength = await _database.ListLengthAsync(_importQueueKey);
        var postImportQueueLength = await _database.ListLengthAsync(_postImportQueueKey);
        return new ProcessingQueueSnapshot(ToInt(importQueueLength), ToInt(postImportQueueLength));
    }

    private static int ToInt(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
