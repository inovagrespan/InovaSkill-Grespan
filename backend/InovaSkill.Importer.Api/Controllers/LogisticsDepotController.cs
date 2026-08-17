using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/logistics-depot")]
public sealed class LogisticsDepotController(ImportDbContext db) : ControllerBase
{
    private const int MaximumNameLength = 160;
    private const int MaximumAddressLength = 500;

    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken)
    {
        var depot = await db.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return depot is null ? NotFound(new { message = "O depósito logístico ainda não foi configurado." }) : Ok(ToResponse(depot));
    }

    [HttpPut]
    public async Task<ActionResult> Upsert(
        [FromBody] UpdateLogisticsDepotRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var now = DateTime.UtcNow;
        var depot = await db.LogisticsDepots.SingleOrDefaultAsync(cancellationToken);
        if (depot is null)
        {
            depot = new LogisticsDepot { Id = Guid.NewGuid(), CreatedAt = now };
            db.LogisticsDepots.Add(depot);
        }
        depot.Name = request.Name.Trim();
        depot.Address = request.Address.Trim();
        depot.Latitude = request.Latitude;
        depot.Longitude = request.Longitude;
        depot.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(depot));
    }

    private static string? Validate(UpdateLogisticsDepotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "O nome do depósito é obrigatório.";
        if (request.Name.Trim().Length > MaximumNameLength) return $"O nome deve possuir no máximo {MaximumNameLength} caracteres.";
        if (string.IsNullOrWhiteSpace(request.Address)) return "O endereço do depósito é obrigatório.";
        if (request.Address.Trim().Length > MaximumAddressLength) return $"O endereço deve possuir no máximo {MaximumAddressLength} caracteres.";
        if (request.Latitude is < -90 or > 90) return "A latitude deve estar entre -90 e 90.";
        if (request.Longitude is < -180 or > 180) return "A longitude deve estar entre -180 e 180.";
        return null;
    }

    private static object ToResponse(LogisticsDepot depot) => new
    {
        depot.Id,
        depot.Name,
        depot.Address,
        depot.Latitude,
        depot.Longitude,
        depot.CreatedAt,
        depot.UpdatedAt
    };
}

public sealed record UpdateLogisticsDepotRequest(
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude);
