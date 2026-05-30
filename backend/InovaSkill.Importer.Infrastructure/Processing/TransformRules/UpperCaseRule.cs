using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class UpperCaseRule : ITransformRule
{
    public string Code => "UpperCase";

    public object? Apply(object? value, string? parametersJson)
    {
        return value?.ToString()?.ToUpperInvariant();
    }
}
