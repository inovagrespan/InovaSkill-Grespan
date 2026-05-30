namespace InovaSkill.Importer.Application.Abstractions;

public interface ITransformRule
{
    string Code { get; }
    object? Apply(object? value, string? parametersJson);
}

