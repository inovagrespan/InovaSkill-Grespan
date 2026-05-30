using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class TransformRuleRegistry(IEnumerable<ITransformRule> rules) : ITransformRuleRegistry
{
    private readonly Dictionary<string, ITransformRule> _rules = rules.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public ITransformRule Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Codigo da regra de transformacao nao informado.");
        }

        if (_rules.TryGetValue(code.Trim(), out var rule))
        {
            return rule;
        }

        throw new InvalidOperationException($"Regra de transformacao nao registrada: '{code}'.");
    }
}
