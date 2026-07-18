using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteOptimizationProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_UsesInferredAssignmentCoordinatesWhenRouteEntryHasNoMunicipality()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = RouteImportCodes.DataSource,
            ProcessorKey = "routes",
            Name = "Rotas",
            Type = "XLSX",
            ImportMode = DataSourceImportMode.Snapshot,
            Active = true,
            NextImportVersion = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(),
            DataSourceId = source.Id,
            FileName = "rotas.xlsx",
            FilePath = "rotas.xlsx",
            Status = RouteImportStatus.Completed,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            FinishedAt = DateTime.UtcNow
        };
        var vehicleType = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = "Truck",
            CapacityKg = 10_000m
        };
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(),
            Name = "Agudos",
            StateCode = "SP",
            NormalizedName = MunicipalityNameNormalizer.Normalize("Agudos"),
            CreatedAt = DateTime.UtcNow
        };
        var coordinate = new MunicipalityCoordinate
        {
            Id = Guid.NewGuid(),
            MunicipalityId = municipality.Id,
            Municipality = municipality,
            Latitude = -22.46m,
            Longitude = -48.99m,
            Status = MunicipalityCoordinateStatuses.Resolved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            DataSourceId = source.Id,
            BranchCode = "01",
            ExternalCode = "C1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var route = new Route
        {
            Id = Guid.NewGuid(),
            ImportId = import.Id,
            Name = "Rota Agudos",
            Weekday = "MONDAY",
            VehicleTypeId = vehicleType.Id,
            TotalWeightKg = 1_000m,
            OverallOccupancy = 0.10m,
            OccupancyStatus = RouteOccupancyStatus.Calculated,
            CreatedAt = DateTime.UtcNow
        };
        var entry = new RouteEntry
        {
            Id = Guid.NewGuid(),
            RouteId = route.Id,
            Sequence = 1,
            Name = "Agudos",
            MunicipalityId = null,
            AveragePerDay = 1_000m,
            CreatedAt = DateTime.UtcNow
        };
        var assignment = new RouteCustomerAssignment
        {
            Id = Guid.NewGuid(),
            RouteId = route.Id,
            CustomerId = customer.Id,
            MunicipalityId = municipality.Id,
            Source = RouteCustomerAssignmentSource.InferredByMunicipality,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var run = new RouteOptimizationRun
        {
            Id = Guid.NewGuid(),
            Scope = RouteOptimizationScope.AllRoutes,
            ReferenceDate = new DateOnly(2026, 7, 18),
            RequestedByUserId = 1,
            RequestedFrom = RouteOptimizationRequestedFrom.RouteScreen,
            Status = RouteOptimizationStatus.Pending,
            ProgressStage = RouteOptimizationStatus.Pending,
            Priority = 0,
            AlgorithmVersion = RouteOptimizationCodes.AlgorithmVersion,
            RulesVersion = RouteOptimizationCodes.RulesVersion,
            Confidence = RouteOptimizationConfidence.Insufficient,
            SnapshotImportId = import.Id,
            SnapshotImportVersion = import.Version,
            CreatedAt = DateTime.UtcNow
        };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = RouteOptimizationCodes.JobType,
            RelatedEntityId = run.Id,
            Status = JobExecutionStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        db.AddRange(source, import, vehicleType, municipality, coordinate, customer, route, entry, assignment, run, job);
        await db.SaveChangesAsync();
        var solver = new CapturingSolver();
        var service = new RouteOptimizationProcessingService(
            db,
            [solver],
            NullLogger<RouteOptimizationProcessingService>.Instance);

        await service.ProcessAsync(run.Id, CancellationToken.None);

        var city = Assert.Single(Assert.Single(solver.Problem!.Routes).Cities);
        Assert.NotNull(city.Location);
        Assert.Equal(-22.46m, city.Location!.Latitude);
        Assert.Equal(-48.99m, city.Location.Longitude);
    }

    private sealed class CapturingSolver : IRouteOptimizationSolver
    {
        public RouteOptimizationProblem? Problem { get; private set; }

        public bool CanHandle(RouteOptimizationScope scope) => scope == RouteOptimizationScope.AllRoutes;

        public Task<RouteOptimizationSolution> SolveAsync(
            RouteOptimizationProblem problem,
            CancellationToken cancellationToken)
        {
            Problem = problem;
            var route = Assert.Single(problem.Routes);
            var metrics = new RouteOptimizationMetricsDto(
                route.RouteId,
                route.Name,
                route.TruckModelName,
                route.CapacityKg,
                route.LoadKg,
                route.Occupancy,
                "saudavel",
                route.Cities.Select(city => city.Name).ToArray());

            return Task.FromResult(new RouteOptimizationSolution(
                RouteOptimizationStatus.NoChangeRecommended,
                RouteOptimizationConfidence.Medium,
                [new RouteOptimizationScenarioCandidate(
                    0,
                    RouteOptimizationActionType.NoChange,
                    RouteOptimizationConfidence.Medium,
                    null,
                    metrics,
                    metrics,
                    [new RouteOptimizationReasonDto("NoChange", "Sem alteração recomendada.")],
                    [],
                    [],
                    null)],
                [],
                []));
        }
    }
}
