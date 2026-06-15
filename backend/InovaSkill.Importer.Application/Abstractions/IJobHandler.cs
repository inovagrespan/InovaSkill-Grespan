using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(Job job, CancellationToken cancellationToken);
}
