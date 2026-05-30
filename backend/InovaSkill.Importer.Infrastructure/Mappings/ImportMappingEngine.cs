using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Mappings;

public sealed class ImportMappingEngine(ITransformRuleRegistry transformRuleRegistry) : IImportMappingEngine
{
    public ImportMappingResult MapRow(int rowNumber, IReadOnlyDictionary<string, object?> rawValues, ImportTemplate template)
    {
        var errors = new List<ImportMappingError>();
        var standardValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in template.ColumnMappings.OrderBy(x => x.Id))
        {
            rawValues.TryGetValue(mapping.SourceColumnName, out var rawValue);

            var hasSourceColumn = rawValues.Keys.Any(x => string.Equals(x, mapping.SourceColumnName, StringComparison.OrdinalIgnoreCase));
            var currentValue = rawValue;

            if (!hasSourceColumn && mapping.IsRequired)
            {
                errors.Add(new ImportMappingError(rowNumber, mapping.SourceColumnName, $"Coluna obrigatoria '{mapping.SourceColumnName}' nao encontrada."));
                continue;
            }

            if ((currentValue is null || string.IsNullOrWhiteSpace(currentValue.ToString())) && !string.IsNullOrWhiteSpace(mapping.DefaultValue))
            {
                currentValue = mapping.DefaultValue;
            }

            foreach (var ruleLink in mapping.TransformRules.OrderBy(x => x.Order).ThenBy(x => x.Id))
            {
                try
                {
                    var rule = transformRuleRegistry.Get(ruleLink.TransformRule?.Code ?? string.Empty);
                    currentValue = rule.Apply(currentValue, ruleLink.ParametersJson);
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportMappingError(rowNumber, mapping.SourceColumnName, ex.Message));
                    break;
                }
            }

            if (mapping.IsRequired && (currentValue is null || string.IsNullOrWhiteSpace(currentValue.ToString())))
            {
                errors.Add(new ImportMappingError(rowNumber, mapping.SourceColumnName, $"Campo obrigatorio '{mapping.TargetFieldName}' ficou vazio apos transformacoes."));
                continue;
            }

            standardValues[mapping.TargetFieldName] = currentValue;
        }

        return new ImportMappingResult(rowNumber, standardValues, errors);
    }
}
