using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/vehicle-types")]
public sealed class VehicleTypesController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var items = await dbContext.VehicleTypes.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CapacityKg,
                routeCount = x.Routes.Count
            })
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.VehicleTypes.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CapacityKg,
                routeCount = x.Routes.Count
            })
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromBody] CreateVehicleTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "O nome do tipo de veículo é obrigatório." });
        }

        if (await dbContext.VehicleTypes.AnyAsync(x => x.Name == request.Name.Trim(), cancellationToken))
        {
            return Conflict(new { message = $"Já existe um tipo de veículo com o nome '{request.Name.Trim()}'." });
        }

        var vehicleType = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CapacityKg = request.CapacityKg
        };
        dbContext.VehicleTypes.Add(vehicleType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = vehicleType.Id }, new
        {
            vehicleType.Id,
            vehicleType.Name,
            vehicleType.CapacityKg,
            routeCount = 0
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateVehicleTypeRequest request,
        CancellationToken cancellationToken)
    {
        var vehicleType = await dbContext.VehicleTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (vehicleType is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var trimmed = request.Name.Trim();
            if (await dbContext.VehicleTypes.AnyAsync(x => x.Name == trimmed && x.Id != id, cancellationToken))
            {
                return Conflict(new { message = $"Já existe outro tipo de veículo com o nome '{trimmed}'." });
            }
            vehicleType.Name = trimmed;
        }

        if (request.CapacityKg.HasValue)
        {
            vehicleType.CapacityKg = request.CapacityKg.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            vehicleType.Id,
            vehicleType.Name,
            vehicleType.CapacityKg,
            routeCount = await dbContext.Routes.CountAsync(x => x.VehicleTypeId == id, cancellationToken)
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var vehicleType = await dbContext.VehicleTypes
            .Include(x => x.Routes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (vehicleType is null) return NotFound();

        if (vehicleType.Routes.Count > 0)
        {
            return Conflict(new
            {
                message = $"Não é possível excluir o tipo de veículo '{vehicleType.Name}' pois está vinculado a {vehicleType.Routes.Count} rota(s)."
            });
        }

        dbContext.VehicleTypes.Remove(vehicleType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

public sealed record CreateVehicleTypeRequest(string Name, decimal CapacityKg);
public sealed record UpdateVehicleTypeRequest(string? Name, decimal? CapacityKg);
