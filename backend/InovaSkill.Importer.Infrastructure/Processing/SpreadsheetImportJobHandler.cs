using System.Text.Json;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class SpreadsheetImportJobHandler(IFileImportPipelineProcessor processor) : IJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeCodes.SpreadsheetImport;

    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<SpreadsheetImportJobPayload>(job.PayloadJson, JsonOptions);
        if (payload is null || payload.FileJobId <= 0)
        {
            throw new InvalidOperationException("Payload de importacao invalido.");
        }

        job.UpdateProgress("Processando importacao de planilha", job.ProgressPercent);
        await processor.ProcessJobAsync(payload.FileJobId, cancellationToken);
    }
}
