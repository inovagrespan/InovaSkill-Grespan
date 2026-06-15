using InovaSkill.Importer.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace InovaSkill.Importer.Api.Realtime;

public sealed class SignalRFileJobProgressNotifier(IHubContext<FileJobProgressHub> hubContext) : IFileJobProgressNotifier
{
    public async Task NotifyAsync(long jobId, CancellationToken cancellationToken)
    {
        var payload = new { jobId };
        await hubContext.Clients.All.SendAsync("jobUpdated", payload, cancellationToken);
        await hubContext.Clients.Group(FileJobProgressHub.BuildJobGroup(jobId)).SendAsync("jobUpdated", payload, cancellationToken);
    }
}
