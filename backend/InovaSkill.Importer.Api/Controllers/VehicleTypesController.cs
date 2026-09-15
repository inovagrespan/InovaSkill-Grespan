using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/vehicle-types")]
public sealed class VehicleTypesController(
    ImportDbContext dbContext,
    IOperationalJobQueue? operationalJobQueue = null) : ControllerBase
{
    [HttpGet("fuel-settings")]
    public async Task<ActionResult> GetFuelSettings(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateFuelSettings(cancellationToken);
        return Ok(new { settings.DieselPricePerLiter, settings.UpdatedAt });
    }

    [HttpPut("fuel-settings")]
    public async Task<ActionResult> UpdateFuelSettings(
        [FromBody] UpdateFuelSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DieselPricePerLiter <= 0)
            return BadRequest(new { message = "O preço do diesel deve ser maior que zero." });

        var settings = await GetOrCreateFuelSettings(cancellationToken);
        settings.DieselPricePerLiter = decimal.Round(request.DieselPricePerLiter, 3, MidpointRounding.AwayFromZero);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await QueueCurrentRouteCostsAsync(cancellationToken);
        return Ok(new { settings.DieselPricePerLiter, settings.UpdatedAt });
    }

    [HttpGet("fuel-price/research")]
    public async Task<ActionResult<DieselPriceResearchResult>> ResearchFuelPrice(
        [FromServices] DieselPriceResearchService researchService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await researchService.ResearchAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
    }

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
                x.AxleCount,
                x.MinimumFuelEfficiencyKmPerLiter,
                x.MaximumFuelEfficiencyKmPerLiter,
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
                x.AxleCount,
                x.MinimumFuelEfficiencyKmPerLiter,
                x.MaximumFuelEfficiencyKmPerLiter,
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

        var configurationError = ValidateVehicleCostConfiguration(
            request.AxleCount,
            request.MinimumFuelEfficiencyKmPerLiter,
            request.MaximumFuelEfficiencyKmPerLiter,
            requireComplete: true);
        if (configurationError is not null)
            return BadRequest(new { message = configurationError });

        var vehicleType = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CapacityKg = request.CapacityKg,
            AxleCount = request.AxleCount,
            MinimumFuelEfficiencyKmPerLiter = request.MinimumFuelEfficiencyKmPerLiter,
            MaximumFuelEfficiencyKmPerLiter = request.MaximumFuelEfficiencyKmPerLiter
        };
        dbContext.VehicleTypes.Add(vehicleType);
        await dbContext.SaveChangesAsync(cancellationToken);
        await QueueCurrentRouteCostsAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = vehicleType.Id }, new
        {
            vehicleType.Id,
            vehicleType.Name,
            vehicleType.CapacityKg,
            vehicleType.AxleCount,
            vehicleType.MinimumFuelEfficiencyKmPerLiter,
            vehicleType.MaximumFuelEfficiencyKmPerLiter,
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


        var axleCount = request.AxleCount ?? vehicleType.AxleCount;
        var minimumEfficiency = request.MinimumFuelEfficiencyKmPerLiter ??
            vehicleType.MinimumFuelEfficiencyKmPerLiter;
        var maximumEfficiency = request.MaximumFuelEfficiencyKmPerLiter ??
            vehicleType.MaximumFuelEfficiencyKmPerLiter;
        var updatesCostConfiguration = request.AxleCount.HasValue ||
            request.MinimumFuelEfficiencyKmPerLiter.HasValue ||
            request.MaximumFuelEfficiencyKmPerLiter.HasValue;
        var configurationError = ValidateVehicleCostConfiguration(
            axleCount,
            minimumEfficiency,
            maximumEfficiency,
            requireComplete: updatesCostConfiguration);
        if (configurationError is not null)
            return BadRequest(new { message = configurationError });
        if (request.AxleCount.HasValue) vehicleType.AxleCount = request.AxleCount;
        if (request.MinimumFuelEfficiencyKmPerLiter.HasValue)
            vehicleType.MinimumFuelEfficiencyKmPerLiter = request.MinimumFuelEfficiencyKmPerLiter;
        if (request.MaximumFuelEfficiencyKmPerLiter.HasValue)
            vehicleType.MaximumFuelEfficiencyKmPerLiter = request.MaximumFuelEfficiencyKmPerLiter;

        await dbContext.SaveChangesAsync(cancellationToken);
        await QueueCurrentRouteCostsAsync(cancellationToken);
        return Ok(new
        {
            vehicleType.Id,
            vehicleType.Name,
            vehicleType.CapacityKg,
            vehicleType.AxleCount,
            vehicleType.MinimumFuelEfficiencyKmPerLiter,
            vehicleType.MaximumFuelEfficiencyKmPerLiter,
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
        await QueueCurrentRouteCostsAsync(cancellationToken);
        return Ok();
    }

    private async Task<LogisticsFuelSettings> GetOrCreateFuelSettings(CancellationToken cancellationToken)
    {
        var settings = await dbContext.LogisticsFuelSettings
            .SingleOrDefaultAsync(x => x.Id == LogisticsFuelSettings.CurrentSettingsId, cancellationToken);
        if (settings is not null) return settings;

        settings = new LogisticsFuelSettings { UpdatedAt = DateTimeOffset.UtcNow };
        dbContext.LogisticsFuelSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task QueueCurrentRouteCostsAsync(CancellationToken cancellationToken)
    {
        if (operationalJobQueue is null) return;
        var currentImportId = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentImportId.HasValue) return;
        try
        {
            await operationalJobQueue.TryQueueAsync(
                OperationalJobCodes.RouteCostConsolidation,
                currentImportId.Value,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A configuração já foi persistida; o job pode ser repetido pela Central de Processamentos.
        }
    }

    private static string? ValidateVehicleCostConfiguration(
        int? axleCount,
        decimal? minimumEfficiency,
        decimal? maximumEfficiency,
        bool requireComplete)
    {
        if (!axleCount.HasValue && !minimumEfficiency.HasValue && !maximumEfficiency.HasValue && !requireComplete)
            return null;
        if (!axleCount.HasValue || !minimumEfficiency.HasValue || !maximumEfficiency.HasValue)
            return "Informe eixos, consumo mínimo e consumo máximo do veículo.";
        try
        {
            RouteCostPolicy.ValidateVehicleConfiguration(
                axleCount.Value,
                minimumEfficiency.Value,
                maximumEfficiency.Value);
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }
}

public sealed record CreateVehicleTypeRequest(
    string Name,
    decimal CapacityKg,
    int? AxleCount,
    decimal? MinimumFuelEfficiencyKmPerLiter,
    decimal? MaximumFuelEfficiencyKmPerLiter);
public sealed record UpdateVehicleTypeRequest(
    string? Name,
    decimal? CapacityKg,
    int? AxleCount,
    decimal? MinimumFuelEfficiencyKmPerLiter,
    decimal? MaximumFuelEfficiencyKmPerLiter);
public sealed record UpdateFuelSettingsRequest(decimal DieselPricePerLiter);
