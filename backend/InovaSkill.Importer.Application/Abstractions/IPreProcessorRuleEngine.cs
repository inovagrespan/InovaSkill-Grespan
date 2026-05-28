using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IPreProcessorRuleEngine
{
    PreProcessorExecutionResult Execute(ImportedRow row, IReadOnlyList<PreProcessorTemplateRule> rules);
}

public sealed record PreProcessorExecutionResult(
    ImportedRow Row,
    IReadOnlyList<PreProcessorExecutionError> Errors);

public sealed record PreProcessorExecutionError(
    string Column,
    string Message);


