using InovaSkill.Importer.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class RedisFileJobProgressNotifier(IConnectionMultiplexer multiplexer, IConfiguration configuration) : IFileJobProgressNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISubscriber _subscriber = multiplexer.GetSubscriber();
    private readonly RedisChannel _channel = new(configuration["ImportProcessing:ProgressChannel"] ?? "import:file-job-progress", RedisChannel.PatternMode.Literal);

    public Task NotifyAsync(long jobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(new FileJobProgressNotification(jobId), JsonOptions);
        return _subscriber.PublishAsync(_channel, payload);
    }
}

public sealed record FileJobProgressNotification(long JobId);
