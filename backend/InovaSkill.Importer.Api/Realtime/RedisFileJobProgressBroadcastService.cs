using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

namespace InovaSkill.Importer.Api.Realtime;

public sealed class RedisFileJobProgressBroadcastService(
    IConnectionMultiplexer multiplexer,
    IConfiguration configuration,
    IHubContext<FileJobProgressHub> hubContext,
    ILogger<RedisFileJobProgressBroadcastService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISubscriber _subscriber = multiplexer.GetSubscriber();
    private readonly RedisChannel _channel = new(configuration["ImportProcessing:ProgressChannel"] ?? "import:file-job-progress", RedisChannel.PatternMode.Literal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscription = await _subscriber.SubscribeAsync(_channel);
        subscription.OnMessage(async message =>
        {
            try
            {
                var payload = JsonSerializer.Deserialize<FileJobProgressNotification>(message.Message.ToString(), JsonOptions);
                if (payload is null)
                {
                    logger.LogWarning("Received empty file job progress notification.");
                    return;
                }

                var hubPayload = new { jobId = payload.JobId };
                await hubContext.Clients.All.SendAsync("jobUpdated", hubPayload, stoppingToken);
                await hubContext.Clients.Group(FileJobProgressHub.BuildJobGroup(payload.JobId)).SendAsync("jobUpdated", hubPayload, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Failed to broadcast file job progress notification.");
            }
        });

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await _subscriber.UnsubscribeAsync(_channel);
        }
    }
}
