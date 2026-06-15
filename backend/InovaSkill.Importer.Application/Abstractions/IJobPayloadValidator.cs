using System.Text.Json;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IJobPayloadValidator
{
    string JobType { get; }

    Task ValidateAsync(JsonElement payload, CancellationToken cancellationToken);
}
