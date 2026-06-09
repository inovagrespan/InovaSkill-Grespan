using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Processing.Patterns;

namespace InovaSkill.Importer.Infrastructure.Processing;

internal sealed class PreProcessorTemplateResolver(IEnumerable<ISpreadsheetImportPattern> patterns) : IPreProcessorTemplateResolver
{
    public async Task<ImportTemplate?> ResolveAsync(string fileName, IReadOnlyCollection<string> headers, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        var bestMatch = SpreadsheetImportPatternMatcher.SelectBest(patterns, fileName, headers);
        return bestMatch?.MeetsMinimumConfidence == true
            ? bestMatch.Pattern.CreateTemplate(headers)
            : null;
    }
}
