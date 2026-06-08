namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileJobProgressNotifier
{
    Task NotifyAsync(long jobId, CancellationToken cancellationToken);
}
