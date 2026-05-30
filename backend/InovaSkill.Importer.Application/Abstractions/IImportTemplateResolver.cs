using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IPreProcessorTemplateResolver
{
    Task<ImportTemplate?> ResolveAsync(string fileName, IReadOnlyCollection<string> headers, CancellationToken cancellationToken);
}

