using InovaSkill.Importer.Domain.Entities;
using System.Globalization;
using System.Text;

namespace InovaSkill.Importer.Infrastructure.Processing.Patterns;

internal interface ISpreadsheetImportPattern
{
    string ImportFileTypeCode { get; }
    string DisplayName { get; }
    decimal MinimumConfidence { get; }
    SpreadsheetImportPatternMatch Evaluate(string fileName, IReadOnlyCollection<string> headers);
    ImportTemplate CreateTemplate(IReadOnlyCollection<string>? headers = null);
}

internal sealed record SpreadsheetImportPatternMatch(
    ISpreadsheetImportPattern Pattern,
    decimal Confidence,
    int MatchedRequiredColumns,
    int MissingRequiredColumns,
    int MatchedColumns)
{
    public bool MeetsMinimumConfidence => MissingRequiredColumns == 0 && Confidence >= Pattern.MinimumConfidence;
}

internal static class SpreadsheetImportPatternMatcher
{
    public static SpreadsheetImportPatternMatch? SelectBest(
        IEnumerable<ISpreadsheetImportPattern> patterns,
        string fileName,
        IReadOnlyCollection<string> headers)
    {
        return patterns
            .Select(pattern => pattern.Evaluate(fileName, headers))
            .OrderByDescending(match => match.MeetsMinimumConfidence)
            .ThenByDescending(match => match.Confidence)
            .ThenByDescending(match => match.MatchedRequiredColumns)
            .ThenByDescending(match => match.MatchedColumns)
            .FirstOrDefault();
    }
}

internal abstract class SpreadsheetImportPattern : ISpreadsheetImportPattern
{
    public abstract string ImportFileTypeCode { get; }
    public abstract string DisplayName { get; }
    public virtual decimal MinimumConfidence => 0.85m;
    protected virtual IReadOnlyList<string> FileNameHints => [];
    protected abstract IReadOnlyList<SpreadsheetImportColumnPattern> Columns { get; }

    public SpreadsheetImportPatternMatch Evaluate(string fileName, IReadOnlyCollection<string> headers)
    {
        var normalizedHeaders = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(SpreadsheetImportPatternNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedFileName = SpreadsheetImportPatternNormalizer.Normalize(fileName);

        var requiredColumns = Columns.Count(column => column.IsRequired);
        var matchedColumns = Columns.Count(column => column.Matches(normalizedHeaders));
        var matchedRequiredColumns = Columns.Count(column => column.IsRequired && column.Matches(normalizedHeaders));
        var missingRequiredColumns = requiredColumns - matchedRequiredColumns;
        var requiredCoverage = requiredColumns == 0 ? 1m : (decimal)matchedRequiredColumns / requiredColumns;
        var overallCoverage = Columns.Count == 0 ? 0m : (decimal)matchedColumns / Columns.Count;
        var fileNameBonus = FileNameHints.Any(hint => normalizedFileName.Contains(SpreadsheetImportPatternNormalizer.Normalize(hint), StringComparison.Ordinal))
            ? 0.05m
            : 0m;
        var missingPenalty = missingRequiredColumns == 0
            ? 0m
            : Math.Min(0.30m, missingRequiredColumns * 0.15m);
        var confidence = Math.Max(0m, (requiredCoverage * 0.75m) + (overallCoverage * 0.20m) + fileNameBonus - missingPenalty);

        return new SpreadsheetImportPatternMatch(this, confidence, matchedRequiredColumns, missingRequiredColumns, matchedColumns);
    }

    public ImportTemplate CreateTemplate(IReadOnlyCollection<string>? headers = null)
    {
        var resolvedHeaders = headers ?? Array.Empty<string>();

        return new ImportTemplate
        {
            Name = DisplayName,
            Description = $"Pattern fixo para {DisplayName}.",
            IsActive = true,
            FileNamePattern = string.Join(';', FileNameHints),
            RequiredHeadersCsv = string.Join(',', Columns.Where(column => column.IsRequired).Select(column => column.SourceColumnName)),
            ImportFileType = new ImportFileType
            {
                Code = ImportFileTypeCode,
                Name = DisplayName,
                Description = $"Importacao reconhecida via pattern fixo para {DisplayName}.",
                AllowedExtensions = ".csv,.xlsx",
                IsActive = true
            },
            ColumnMappings = Columns
                .Select((column, index) => column.CreateMapping(index + 1, resolvedHeaders))
                .ToList()
        };
    }
}

internal sealed record SpreadsheetImportColumnPattern(
    string SourceColumnName,
    string TargetFieldName,
    bool IsRequired,
    IReadOnlyList<string> AcceptedHeaders,
    string? DefaultValue = null,
    IReadOnlyList<SpreadsheetImportTransformRuleConfig>? TransformRules = null)
{
    private readonly IReadOnlySet<string> _normalizedAcceptedHeaders = AcceptedHeaders
        .Append(SourceColumnName)
        .Select(SpreadsheetImportPatternNormalizer.Normalize)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool Matches(IReadOnlySet<string> normalizedHeaders)
    {
        return _normalizedAcceptedHeaders.Any(normalizedHeaders.Contains);
    }

    public string ResolveSourceHeader(IReadOnlyCollection<string>? headers)
    {
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var normalizedHeader = SpreadsheetImportPatternNormalizer.Normalize(header);
                if (_normalizedAcceptedHeaders.Contains(normalizedHeader))
                {
                    return header.Trim();
                }
            }
        }

        return SourceColumnName;
    }

    public ImportColumnMapping CreateMapping(int order, IReadOnlyCollection<string>? headers = null)
    {
        var resolvedSourceHeader = ResolveSourceHeader(headers);

        return new ImportColumnMapping
        {
            Id = order,
            SourceColumnName = resolvedSourceHeader,
            TargetFieldName = TargetFieldName,
            IsRequired = IsRequired,
            DefaultValue = DefaultValue,
            TransformRules = (TransformRules ?? [])
                .Select((rule, index) => new ColumnMappingTransformRule
                {
                    Id = index + 1,
                    Order = index + 1,
                    ParametersJson = rule.ParametersJson,
                    TransformRule = new TransformRule
                    {
                        Code = rule.Code,
                        Name = rule.Code,
                        Description = rule.Code,
                        IsActive = true
                    }
                })
                .ToList()
        };
    }
}

internal sealed record SpreadsheetImportTransformRuleConfig(string Code, string? ParametersJson = null);

internal static class SpreadsheetImportPatternNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSeparator = false;

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim();
    }
}
