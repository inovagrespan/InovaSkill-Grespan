using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/findings")]
public sealed class FindingsController(
    ImportDbContext dbContext) : ControllerBase
{
    [HttpGet("{findingId:guid}")]
    public async Task<ActionResult> Get(
        Guid findingId,
        CancellationToken cancellationToken)
    {
        var finding = await dbContext.Findings.AsNoTracking()
            .Where(x => x.Id == findingId)
            .Select(x => new
            {
                x.Id,
                x.Fingerprint,
                x.Title,
                x.Description,
                x.SubjectType,
                x.SubjectId,
                x.SubjectLabel,
                x.DetectedAt,
                evidences = dbContext.FindingEvidences.AsNoTracking()
                    .Where(e => e.FindingId == x.Id)
                    .OrderBy(e => e.Name)
                    .Select(e => new
                    {
                        e.Name,
                        e.Value,
                        e.ReferenceValue,
                        e.Unit,
                        e.Description,
                        e.SourceType,
                        e.SourceId,
                        e.ObservedAt
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return finding is null ? NotFound() : Ok(finding);
    }
}
