using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class LowerCaseRule : ITransformRule
{
    public string Code => "LowerCase";

    public object? Apply(object? value, string? parametersJson)
    {
        return value?.ToString()?.ToLowerInvariant();
    }
}
