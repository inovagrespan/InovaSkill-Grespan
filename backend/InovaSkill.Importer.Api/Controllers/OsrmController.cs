using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/osrm")]
public sealed class OsrmController(ImportDbContext db, IOsrmTableClient osrm) : ControllerBase
{
    [HttpGet("health")]
    public async Task<ActionResult> Health(CancellationToken cancellationToken)
    {
        var depot = await db.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (depot is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "UNAVAILABLE", message = "Configure o depósito logístico antes de testar o OSRM." });

        var healthy = await osrm.IsHealthyAsync(depot.Latitude, depot.Longitude, cancellationToken);
        return healthy
            ? Ok(new { status = "HEALTHY" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "UNAVAILABLE", message = "O OSRM não conseguiu localizar o depósito no mapa configurado." });
    }
}
