using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class SpreadsheetImportJobPayloadValidator(ImportDbContext dbContext) : IJobPayloadValidator
{
    public string JobType => JobTypeCodes.SpreadsheetImport;

    public async Task ValidateAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!payload.TryGetProperty("fileJobId", out var fileJobIdElement) ||
            !fileJobIdElement.TryGetInt64(out var fileJobId) ||
            fileJobId <= 0)
        {
            throw new InvalidOperationException("Payload de importacao deve informar fileJobId valido.");
        }

        var exists = await dbContext.FileJobs.AnyAsync(x => x.Id == fileJobId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"FileJob {fileJobId} nao foi encontrado.");
        }
    }
}
