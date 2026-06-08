using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class NullFileJobProgressNotifier : IFileJobProgressNotifier
{
    public Task NotifyAsync(long jobId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
