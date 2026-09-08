using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class DailyRouteOptimizationProcessor(
    ImportDbContext db,
    IOsrmDailyMatrixService matrixService,
    IDailyRouteOptimizer optimizer) : IProgressReportingOperationalJobProcessor
{
    public string JobType => OperationalJobCodes.DailyRouteOptimization;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A otimização diária exige o identificador da execução.");

    public async Task ProcessAsync(Guid importId, Guid jobExecutionId, CancellationToken cancellationToken)
    {
        var job = await db.JobExecutions.SingleAsync(item => item.Id == jobExecutionId, cancellationToken);
        using var parameters = JsonDocument.Parse(job.ParametersJson);
        var weekday = parameters.RootElement.GetProperty("weekday").GetString()?.Trim().ToUpperInvariant()
            ?? throw new ArgumentException("O dia da semana é obrigatório.");
        job.ProgressPercent = 10;
        job.ProgressMessage = "Montando matriz rodoviária";
        await db.SaveChangesAsync(cancellationToken);

        var matrix = await matrixService.GetForDayAsync(importId, weekday, cancellationToken);
        var routes = await db.Routes.AsNoTracking()
            .Where(route => route.ImportId == importId && route.Weekday == weekday)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.VehicleTypeId,
                VehicleType = route.VehicleType!.Name,
                CapacityKg = route.VehicleType.CapacityKg,
                Entries = route.Entries.OrderBy(entry => entry.Sequence).Select(entry => new
                {
                    entry.MunicipalityId,
                    entry.Name,
                    entry.AveragePerDay,
                    entry.Deliveries,
                    entry.Sequence
                }).ToArray()
            }).ToArrayAsync(cancellationToken);
        var invalidVehicle = routes.FirstOrDefault(route => route.CapacityKg is null or <= 0);
        if (invalidVehicle is not null)
        {
            SaveResult(job, new DailyRouteOptimizationResult(importId, weekday,
                DailyRouteOptimizationStatuses.InsufficientData, DailyRouteOptimizationPolicy.RulesVersion,
                matrix.Source, $"A rota {invalidVehicle.Name} não possui capacidade de veículo válida.",
                new(0, 0, 0, 0, 0), new(0, 0, 0, 0, 0), []));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        if (routes.SelectMany(route => route.Entries).Any(entry => entry.MunicipalityId is null))
        {
            SaveResult(job, new DailyRouteOptimizationResult(importId, weekday,
                DailyRouteOptimizationStatuses.InsufficientData, DailyRouteOptimizationPolicy.RulesVersion,
                matrix.Source, "Há cidade sem município identificado.",
                new(0, 0, 0, 0, 0), new(0, 0, 0, 0, 0), []));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var importedStops = DailyOptimizationStopConsolidator.Consolidate(routes
            .SelectMany(route => route.Entries.Select(entry => new DailyOptimizationStop(
                entry.MunicipalityId!.Value, entry.Name, entry.AveragePerDay, entry.Deliveries, route.Id, entry.Sequence))));
        var stopByMunicipality = importedStops.ToDictionary(stop => stop.MunicipalityId);
        var stops = matrix.Points.Skip(1).Select(point => stopByMunicipality.TryGetValue(point.Id, out var stop)
            ? stop
            : throw new InvalidOperationException("A matriz contém município que não pertence ao problema diário.")).ToArray();
        var ownVehicles = routes.Select(route => new DailyOptimizationVehicle(route.Id, route.VehicleTypeId,
            route.Name, route.VehicleType, route.CapacityKg!.Value, false)).ToArray();
        var rentalTypes = await db.VehicleTypes.AsNoTracking().Where(type => type.CapacityKg > 0)
            .OrderBy(type => type.CapacityKg).Select(type => new DailyOptimizationVehicle(null, type.Id,
                type.Name, type.Name, type.CapacityKg!.Value, true)).ToArrayAsync(cancellationToken);
        var dieselPricePerLiter = await db.LogisticsFuelSettings.AsNoTracking()
            .Where(settings => settings.Id == LogisticsFuelSettings.CurrentSettingsId)
            .Select(settings => (decimal?)settings.DieselPricePerLiter)
            .SingleOrDefaultAsync(cancellationToken)
            ?? LogisticsFuelSettings.DefaultDieselPricePerLiter;

        job.ProgressPercent = 55;
        job.ProgressMessage = "Otimizando distribuição e sequência";
        await db.SaveChangesAsync(cancellationToken);
        var problem = new DailyRouteOptimizationProblem(importId, weekday, dieselPricePerLiter, matrix.Points,
            matrix.DurationsSeconds, matrix.DistancesMeters, stops, ownVehicles, rentalTypes);
        SaveResult(job, optimizer.Optimize(problem, matrix.Source));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void SaveResult(Domain.Entities.JobExecution job, DailyRouteOptimizationResult result)
    {
        job.ResultJson = JsonSerializer.Serialize(result);
        job.ProgressPercent = 95;
        job.ProgressMessage = result.Status;
    }
}
