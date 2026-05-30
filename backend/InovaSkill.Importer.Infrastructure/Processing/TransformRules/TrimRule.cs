using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class TrimRule : ITransformRule
{
    public string Code => "Trim";

    public object? Apply(object? value, string? parametersJson)
    {
        return value?.ToString()?.Trim();
    }
}
