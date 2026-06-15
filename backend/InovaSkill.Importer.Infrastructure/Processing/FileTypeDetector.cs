using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.Processing.Patterns;

namespace InovaSkill.Importer.Infrastructure.Processing;

internal sealed class FileTypeDetector(IEnumerable<ISpreadsheetImportPattern> patterns) : IFileTypeDetector
{
    public string? DetectCode(IReadOnlyDictionary<string, string> row)
    {
        var bestMatch = SpreadsheetImportPatternMatcher.SelectBest(patterns, string.Empty, row.Keys.ToArray());
        return bestMatch?.MeetsMinimumConfidence == true
            ? bestMatch.Pattern.ImportFileTypeCode
            : null;
    }
}
