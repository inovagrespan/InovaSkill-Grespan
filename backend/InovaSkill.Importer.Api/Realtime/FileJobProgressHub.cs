using Microsoft.AspNetCore.SignalR;

namespace InovaSkill.Importer.Api.Realtime;

public sealed class FileJobProgressHub : Hub
{
    public Task SubscribeJob(long jobId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, BuildJobGroup(jobId));
    }

    public Task UnsubscribeJob(long jobId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildJobGroup(jobId));
    }

    internal static string BuildJobGroup(long jobId)
    {
        return $"job:{jobId}";
    }
}
