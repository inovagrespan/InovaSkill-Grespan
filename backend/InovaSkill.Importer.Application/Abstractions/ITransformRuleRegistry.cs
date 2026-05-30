namespace InovaSkill.Importer.Application.Abstractions;

public interface ITransformRuleRegistry
{
    ITransformRule Get(string code);
}

