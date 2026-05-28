using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(
    IFileUploadService fileUploadService,
    ImportDbContext dbContext) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<UploadResponse>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        await using var stream = file.OpenReadStream();
        var fileJobId = await fileUploadService.UploadAndCreateJobAsync(stream, file.FileName, cancellationToken);
        return Ok(new UploadResponse(fileJobId));
    }

    [HttpPost("jobs/{jobId:long}/retry")]
    public async Task<ActionResult> RetryJob(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status == Domain.Enums.FileJobStatus.Importing)
        {
            if (job.FileType == Domain.Enums.FileType.Customers)
            {
                await dbContext.Customers.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
            else if (job.FileType == Domain.Enums.FileType.Products)
            {
                await dbContext.Products.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
            else if (job.FileType == Domain.Enums.FileType.Orders)
            {
                await dbContext.Orders.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
        }

        // if (!string.IsNullOrWhiteSpace(job.NormalizedFilePath) &&
        //     job.Status != Domain.Enums.FileJobStatus.ReadyToImport)
        // {
        //     try
        //     {
        //         if (System.IO.File.Exists(job.NormalizedFilePath))
        //         {
        //             System.IO.File.Delete(job.NormalizedFilePath);
        //         }
        //     }
        //     catch
        //     {
        //         // best effort
        //     }
        //     job.NormalizedFilePath = string.Empty;
        // }

        if (job.Status == Domain.Enums.FileJobStatus.ValidationFailed)
        {
            job.FileType = Domain.Enums.FileType.Unknown;
        }

        job.RequeueManually();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<PagedResult<FileJobDto>>> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var baseQuery = dbContext.FileJobs.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);

        var jobs = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FileJobDto(
                x.Id,
                x.FilePath,
                x.FileType,
                x.Status,
                x.CreatedAt,
                dbContext.ImportErrors.Count(e => e.FileJobId == x.Id),
                x.CurrentStep,
                x.ProgressPercent,
                x.ProcessedRows,
                x.TotalRows))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<FileJobDto>(page, pageSize, total, jobs));
    }

    [HttpGet("jobs/{jobId:long}/errors")]
    public async Task<ActionResult<PagedResult<ImportErrorDto>>> GetJobErrors(
        long jobId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);

        var exists = await dbContext.FileJobs.AnyAsync(x => x.Id == jobId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var baseQuery = dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId);

        var total = await baseQuery.CountAsync(cancellationToken);

        var errors = await dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId)
            .OrderBy(x => x.RowNumber)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ImportErrorDto(x.Id, x.FileJobId, x.RowNumber, x.Column, x.Message, x.RecordIdentifier))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<ImportErrorDto>(page, pageSize, total, errors));
    }
}

