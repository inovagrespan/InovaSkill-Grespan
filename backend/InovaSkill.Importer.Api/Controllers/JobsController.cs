using System.Text.Json;
using Hangfire;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing.Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(
    ImportDbContext dbContext,
    IJobService jobService,
    IBackgroundJobClient backgroundJobClient) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<ActionResult> ListJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = dbContext.Jobs.AsNoTracking().OrderByDescending(j => j.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 10, 200))
            .Take(Math.Clamp(pageSize, 10, 200))
            .Select(j => new
            {
                j.Id,
                j.Type,
                status = j.Status.ToString(),
                j.ProgressPercent,
                j.CurrentStep,
                j.CreatedAt,
                j.StartedAt,
                j.FinishedAt,
                j.UserId,
                j.PayloadJson,
                j.ResultJson,
                j.Error,
                j.RetryCount,
                elapsedSeconds = j.StartedAt != null
                    ? (j.FinishedAt != null
                        ? (int)(j.FinishedAt.Value - j.StartedAt.Value).TotalSeconds
                        : j.Status == Domain.Enums.JobStatus.Queued || j.Status == Domain.Enums.JobStatus.Processing || j.Status == Domain.Enums.JobStatus.Pending
                            ? (int)(DateTime.UtcNow - j.StartedAt.Value).TotalSeconds
                            : 0)
                    : 0
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost("enqueue")]
    public async Task<ActionResult> Enqueue([FromBody] EnqueueGenericJobRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
            return BadRequest("Tipo de job obrigatório.");

        string payloadJson;
        try
        {
            payloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : JsonDocument.Parse(request.PayloadJson).RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return BadRequest("PayloadJson inválido.");
        }

        var job = new Job
        {
            Type = request.Type.Trim(),
            Status = JobStatus.Pending,
            PayloadJson = payloadJson,
            UserId = string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(ct);
        job.MarkQueued();
        await dbContext.SaveChangesAsync(ct);

        backgroundJobClient.Enqueue<GenericJobRunner>(x => x.RunAsync(job.Id, CancellationToken.None));

        return Ok(new { job.Id, job.Type, status = job.Status.ToString() });
    }

    [HttpPost("{jobId:long}/retry")]
    public async Task<ActionResult> Retry(long jobId, [FromBody] EnqueueGenericJobRequest? request, CancellationToken ct = default)
    {
        var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request?.PayloadJson))
        {
            try { JsonDocument.Parse(request.PayloadJson); job.PayloadJson = request.PayloadJson; }
            catch { return BadRequest("PayloadJson inválido."); }
        }

        // If it's a spreadsheet import, also retry the associated FileJob
        if (string.Equals(job.Type, "SpreadsheetImport", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = JsonSerializer.Deserialize<SpreadsheetImportJobPayload>(job.PayloadJson, JsonOptions);
            if (parsed?.FileJobId > 0)
            {
                var fileJob = await dbContext.FileJobs.FirstOrDefaultAsync(f => f.Id == parsed.FileJobId, ct);
                if (fileJob != null)
                {
                    if (fileJob.Status is FileJobStatus.Importing or FileJobStatus.PreProcessing or FileJobStatus.Validating)
                        await CleanupImportedDataAsync(fileJob, ct);
                    if (fileJob.Status == FileJobStatus.ValidationFailed) fileJob.ImportFileTypeCode = null;

                    fileJob.RequeueManually();
                    await dbContext.SaveChangesAsync(ct);
                }
            }
        }

        job.MarkQueued();
        await dbContext.SaveChangesAsync(ct);

        if (string.Equals(job.Type, "SpreadsheetImport", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = JsonSerializer.Deserialize<SpreadsheetImportJobPayload>(job.PayloadJson, JsonOptions);
            if (parsed?.FileJobId > 0)
                backgroundJobClient.Enqueue<SpreadsheetImportJobRunner>(x => x.RunAsync(parsed.FileJobId, CancellationToken.None));
        }
        else
        {
            backgroundJobClient.Enqueue<GenericJobRunner>(x => x.RunAsync(job.Id, CancellationToken.None));
        }

        return Ok(new { job.Id, status = job.Status.ToString() });
    }

    [HttpPost("{jobId:long}/cancel")]
    public async Task<ActionResult> Cancel(long jobId, CancellationToken ct = default)
    {
        var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return NotFound();

        job.MarkCancelled("Cancelado manualmente");
        await dbContext.SaveChangesAsync(ct);

        // Also cancel the associated FileJob if it's a spreadsheet import
        if (string.Equals(job.Type, "SpreadsheetImport", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = JsonSerializer.Deserialize<SpreadsheetImportJobPayload>(job.PayloadJson, JsonOptions);
            if (parsed?.FileJobId > 0)
            {
                var fileJob = await dbContext.FileJobs.FirstOrDefaultAsync(f => f.Id == parsed.FileJobId, ct);
                if (fileJob != null && fileJob.Status is not (FileJobStatus.Completed or FileJobStatus.Failed or FileJobStatus.Cancelled))
                {
                    fileJob.MarkCancelled();
                    await dbContext.SaveChangesAsync(ct);
                }
            }
        }

        return Ok(new { job.Id, status = job.Status.ToString() });
    }

    private async Task CleanupImportedDataAsync(FileJob fileJob, CancellationToken ct)
    {
        var code = fileJob.ImportFileTypeCode?.ToUpperInvariant();
        if (code == ImportFileTypeCodes.Customers) await dbContext.Customers.Where(x => x.SourceFileJobId == fileJob.Id).ExecuteDeleteAsync(ct);
        else if (code == ImportFileTypeCodes.Products) await dbContext.Products.Where(x => x.SourceFileJobId == fileJob.Id).ExecuteDeleteAsync(ct);
        else if (code == ImportFileTypeCodes.FinancialEntry) await dbContext.Orders.Where(x => x.SourceFileJobId == fileJob.Id).ExecuteDeleteAsync(ct);
        else if (code == ImportFileTypeCodes.SalesInvoice) await dbContext.CommercialTransactions.Where(x => x.SourceFileJobId == fileJob.Id).ExecuteDeleteAsync(ct);
        else if (code == ImportFileTypeCodes.RoutePlanning)
        {
            var imports = await dbContext.RoutePlanningImports.Where(x => x.SourceFileJobId == fileJob.Id).ToListAsync(ct);
            if (imports.Count > 0) dbContext.RoutePlanningImports.RemoveRange(imports);
        }
    }
}

public sealed record EnqueueGenericJobRequest(string? Type, string? PayloadJson, string? UserId);
