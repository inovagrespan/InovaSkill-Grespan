using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class PreProcessorTemplateResolver(ImportDbContext dbContext) : IPreProcessorTemplateResolver
{
    public async Task<PreProcessorTemplate?> ResolveAsync(string fileName, IReadOnlyCollection<string> headers, CancellationToken cancellationToken)
    {
        var activeTemplates = await dbContext.PreProcessorTemplates
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeTemplates.Count == 0)
        {
            return null;
        }

        var normalizedHeaders = headers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var template in activeTemplates)
        {
            if (!MatchesHeaders(template, normalizedHeaders))
            {
                continue;
            }

            return template;
        }

        return null;
    }

    private static bool MatchesHeaders(PreProcessorTemplate template, HashSet<string> normalizedHeaders)
    {
        var requiredHeaders = template.RequiredHeadersCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .ToList();

        if (requiredHeaders.Count == 0)
        {
            return true;
        }

        return requiredHeaders.All(normalizedHeaders.Contains);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

