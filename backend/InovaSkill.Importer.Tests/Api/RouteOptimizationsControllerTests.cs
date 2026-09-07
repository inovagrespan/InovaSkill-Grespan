using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Claims;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.Api;

public sealed class RouteOptimizationsControllerTests
{
    [Fact]
    public void Controller_HasSingleConstructorForDependencyInjection()
    {
        Assert.Single(typeof(RouteOptimizationsController).GetConstructors());
    }

    [Fact]
    public async Task ListAndDetail_ReturnConsistentMetricsAndOrderedDistribution()
    {
        await using var db = Context();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "routes.xlsx", FilePath = "routes.xlsx", Status = RouteImportStatus.Completed, FinishedAt = now, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now };
        var type = new VehicleType { Id = Guid.NewGuid(), Name = "Acelo", CapacityKg = 3_300m };
        var municipality = new Municipality { Id = Guid.NewGuid(), Name = "Marília", NormalizedName = "MARILIA", StateCode = "SP", CreatedAt = now };
        var result = new DailyRouteOptimizationResult
        {
            Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = job.Id, Weekday = "MONDAY",
            Status = DailyRouteOptimizationStatuses.Optimized, CurrentDistanceMeters = 20_000,
            ProposedDistanceMeters = 15_000, ProposedDurationSeconds = 3_600,
            CurrentVehicleCount = 1, ProposedVehicleCount = 2, AdditionalVehicleCount = 2,
            AdditionalCapacityKg = 6_600, TotalWeightKg = 12_000, CreatedAt = now
        };
        var vehicle = new DailyRouteOptimizationVehicle
        {
            Id = Guid.NewGuid(), ResultId = result.Id, VehicleTypeId = type.Id, Sequence = 0,
            IsAdditional = true, CapacityKg = 3_300, LoadKg = 3_000, Occupancy = 3_000m / 3_300m,
            Stops = [new DailyRouteOptimizationStop { Id = Guid.NewGuid(), MunicipalityId = municipality.Id, Sequence = 0, WeightKg = 3_000 }]
        };
        var idleVehicle = new DailyRouteOptimizationVehicle
        {
            Id = Guid.NewGuid(), ResultId = result.Id, VehicleTypeId = type.Id, Sequence = 1,
            IsIdle = true, CapacityKg = 3_300, LoadKg = 0, Occupancy = 0
        };
        result.Vehicles.Add(vehicle);
        result.Vehicles.Add(idleVehicle);
        db.AddRange(source, import, job, type, municipality, result);
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeLauncher());

        var listJson = Json(await controller.List(null, null, default));
        var detailJson = Json(await controller.Get(result.Id, default));

        var summary = listJson.RootElement.GetProperty("items")[0];
        Assert.True(listJson.RootElement.GetProperty("isCurrentSnapshot").GetBoolean());
        Assert.Equal(job.Id, listJson.RootElement.GetProperty("latestExecution").GetProperty("Id").GetGuid());
        Assert.Equal("Completed", listJson.RootElement.GetProperty("latestExecution").GetProperty("Status").GetString());
        Assert.Equal(15_000m, summary.GetProperty("ProposedDistanceMeters").GetDecimal());
        Assert.Equal(1, summary.GetProperty("proposedVehicleCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("additionalVehicleCount").GetInt32());
        Assert.Equal(3_300m, summary.GetProperty("additionalCapacityKg").GetDecimal());
        Assert.Equal(summary.GetProperty("additionalVehicleCount").GetInt32(), detailJson.RootElement.GetProperty("additionalVehicleCount").GetInt32());
        Assert.Equal("Acelo", detailJson.RootElement.GetProperty("vehicles")[0].GetProperty("vehicleType").GetString());
        Assert.Equal("Marília", detailJson.RootElement.GetProperty("vehicles")[0].GetProperty("stops")[0].GetProperty("municipality").GetString());
    }

    [Fact]
    public async Task ListWithoutPublishedSnapshot_ReturnsEmptyResult()
    {
        await using var db = Context();
        var response = await Controller(db, new FakeLauncher()).List(null, null, default);
        var json = Json(response);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("snapshotId").ValueKind);
        Assert.False(json.RootElement.GetProperty("isCurrentSnapshot").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("latestExecution").ValueKind);
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData(AppUserRoles.Diretor)]
    [InlineData(AppUserRoles.Vendas)]
    public async Task Remediation_ForLegacyResult_EvaluatesAllIssuesAndAllowsBusinessProfilesToCorrectCurrentSnapshot(string role)
    {
        await using var db = Context();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "routes.xlsx", FilePath = "routes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_000 };
        var route = new InovaSkill.Importer.Domain.Entities.Route { Id = Guid.NewGuid(), ImportId = import.Id, Name = "ROTA", Weekday = "MONDAY", VehicleTypeId = vehicle.Id, VehicleCapacityKgSnapshot = 10_000, CreatedAt = now };
        route.Entries.Add(new RouteEntry { Id = Guid.NewGuid(), RouteId = route.Id, Name = "SEM VÍNCULO", AveragePerDay = 0, CreatedAt = now });
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, Queue = "default", ParametersJson = "{}", RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now };
        var result = new DailyRouteOptimizationResult { Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = job.Id, Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.InsufficientData, Reason = "legado", CreatedAt = now };
        db.AddRange(source, import, vehicle, route, job, result);
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeLauncher(), municipalityProvider: new EmbeddedMunicipalityCoordinateProvider());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, role)], "test"))
            }
        };

        var json = Json(await controller.GetRemediation(result.Id, default));

        Assert.False(json.RootElement.GetProperty("readOnly").GetBoolean());
        Assert.True(json.RootElement.GetProperty("canResolve").GetBoolean());
        Assert.Equal(2, json.RootElement.GetProperty("issueCount").GetInt32());
        var issueCodes = json.RootElement.GetProperty("routes")[0].GetProperty("issues")
            .EnumerateArray().Select(item => item.GetProperty("Code").GetString()).ToArray();
        Assert.Contains(DailyRouteOptimizationIssueCodes.MunicipalityNotLinked, issueCodes);
        Assert.Contains(DailyRouteOptimizationIssueCodes.InvalidStopWeight, issueCodes);
    }

    [Fact]
    public async Task Remediation_RejectsExclusionForNegativeWeight()
    {
        await using var db = Context();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "routes.xlsx", FilePath = "routes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_000 };
        var route = new InovaSkill.Importer.Domain.Entities.Route { Id = Guid.NewGuid(), ImportId = import.Id, Name = "ROTA", Weekday = "MONDAY", VehicleTypeId = vehicle.Id, VehicleCapacityKgSnapshot = 10_000, CreatedAt = now };
        var entry = new RouteEntry { Id = Guid.NewGuid(), RouteId = route.Id, Name = "NEGATIVA", AveragePerDay = -1, CreatedAt = now };
        route.Entries.Add(entry);
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, Queue = "default", ParametersJson = "{}", RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now };
        var result = new DailyRouteOptimizationResult { Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = job.Id, Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.InsufficientData, CreatedAt = now };
        db.AddRange(source, import, vehicle, route, job, result);
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeLauncher(), municipalityProvider: new EmbeddedMunicipalityCoordinateProvider());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, AppUserRoles.Logistica),
                    new Claim(ClaimTypes.NameIdentifier, "10")
                ], "test"))
            }
        };
        var request = new CreateRouteOptimizationRemediationRequest(import.Id,
        [
            new RouteOptimizationResolutionRequest(RouteImportCorrectionKinds.ExcludeStop, route.Id, entry.Id,
                null, null, null, null, null, null, null)
        ]);

        var response = await controller.CreateRemediation(result.Id, request, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Contains("exatamente zero", JsonSerializer.Serialize(badRequest.Value));
        Assert.Empty(db.RouteImportCorrections);
    }

    [Fact]
    public async Task Remediation_AcceptsPtBrWeightAndQueuesDerivedVersionWithoutChangingParent()
    {
        await using var db = Context();
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, NextImportVersion = 2, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "routes.xlsx", FilePath = "immutable/routes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        source.CurrentImportId = import.Id;
        var user = new AppUser { Id = 10, Name = "logistica", Email = "logistica@test", Role = AppUserRoles.Logistica, PasswordHash = "hash", CreatedAt = now };
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_000 };
        var route = new InovaSkill.Importer.Domain.Entities.Route { Id = Guid.NewGuid(), ImportId = import.Id, Name = "ROTA", Weekday = "MONDAY", VehicleTypeId = vehicle.Id, VehicleCapacityKgSnapshot = 10_000, CreatedAt = now };
        var entry = new RouteEntry { Id = Guid.NewGuid(), RouteId = route.Id, Name = "MARILIA SEM ACENTO", AveragePerDay = 0, CreatedAt = now };
        route.Entries.Add(entry);
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, Queue = "default", ParametersJson = "{}", RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now };
        var result = new DailyRouteOptimizationResult { Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = job.Id, Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.InsufficientData, CreatedAt = now };
        db.AddRange(source, import, user, vehicle, route, job, result);
        await db.SaveChangesAsync();
        var dispatcher = new RecordingDispatcher();
        var controller = Controller(db, new FakeLauncher(), new FakeLifecycle(db), dispatcher, new EmbeddedMunicipalityCoordinateProvider());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, AppUserRoles.Logistica),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                ], "test"))
            }
        };
        using var weightJson = JsonDocument.Parse("\"1.234,567\"");
        var request = new CreateRouteOptimizationRemediationRequest(import.Id,
        [
            new RouteOptimizationResolutionRequest(RouteImportCorrectionKinds.SetWeight, route.Id, entry.Id,
                null, null, null, weightJson.RootElement.Clone(), null, null, null),
            new RouteOptimizationResolutionRequest(RouteImportCorrectionKinds.LinkMunicipality, route.Id, entry.Id,
                null, "3529005", null, null, null, null, null)
        ]);

        var response = await controller.CreateRemediation(result.Id, request, default);

        Assert.IsType<AcceptedResult>(response);
        var derived = await db.RouteImports.SingleAsync(item => item.DerivedFromImportId == import.Id);
        Assert.Equal(import.FilePath, derived.FilePath);
        Assert.Equal(1_234.567m, await db.RouteImportCorrections.Where(item => item.DerivedImportId == derived.Id &&
            item.Kind == RouteImportCorrectionKinds.SetWeight).Select(item => item.CorrectedWeightKg).SingleAsync());
        Assert.Equal(0, entry.AveragePerDay);
        Assert.Equal(derived.Id, dispatcher.ImportId);
    }

    [Fact]
    public async Task ListForHistoricalDate_ReturnsPersistedSnapshotAsReadOnlyWithItsLatestFailure()
    {
        await using var db = Context();
        var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var historical = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "old.xlsx", FilePath = "old.xlsx", Status = RouteImportStatus.Completed, FinishedAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc), CreatedAt = now };
        var current = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 2, FileName = "new.xlsx", FilePath = "new.xlsx", Status = RouteImportStatus.Completed, FinishedAt = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), CreatedAt = now };
        source.CurrentImportId = current.Id;
        var oldJob = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, RelatedEntityId = historical.Id, Status = JobExecutionStatus.Completed, CreatedAt = now.AddMinutes(-2) };
        var failedJob = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, RelatedEntityId = historical.Id, Status = JobExecutionStatus.Failed, ErrorMessage = "OSRM indisponível", CreatedAt = now.AddMinutes(-1), FinishedAt = now };
        var result = new DailyRouteOptimizationResult { Id = Guid.NewGuid(), RouteImportId = historical.Id, JobExecutionId = oldJob.Id, Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.NoImprovement, Reason = "Sem melhoria", CurrentVehicleCount = 1, ProposedVehicleCount = 1, CreatedAt = now };
        db.AddRange(source, historical, current, oldJob, failedJob, result);
        await db.SaveChangesAsync();

        var json = Json(await Controller(db, new FakeLauncher())
            .List(new DateOnly(2026, 8, 10), null, default));

        Assert.Equal(historical.Id, json.RootElement.GetProperty("snapshotId").GetGuid());
        Assert.False(json.RootElement.GetProperty("isCurrentSnapshot").GetBoolean());
        Assert.Equal(failedJob.Id, json.RootElement.GetProperty("latestExecution").GetProperty("Id").GetGuid());
        Assert.Equal("Failed", json.RootElement.GetProperty("latestExecution").GetProperty("Status").GetString());
        Assert.Equal("OSRM indisponível", json.RootElement.GetProperty("latestExecution").GetProperty("ErrorMessage").GetString());
        Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Simulate_QueuesVersionedDailyOptimizationJob()
    {
        await using var db = Context();
        var launcher = new FakeLauncher();
        var response = await Controller(db, launcher).Simulate(default);

        var accepted = Assert.IsType<AcceptedResult>(response);
        Assert.NotNull(accepted.Value);
        Assert.Equal(OperationalJobCodes.DailyRouteOptimization, launcher.Request?.JobType);
        Assert.Equal(JobExecutionTrigger.Manual, launcher.Request?.Trigger);
        Assert.Equal("{}", launcher.Request?.ParametersJson);
    }

    [Fact]
    public async Task Simulate_WithWeekdays_PersistsOnlyRequestedDaysInJobContract()
    {
        await using var db = Context();
        var launcher = new FakeLauncher();
        var controller = Controller(db, launcher);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var payload = Encoding.UTF8.GetBytes("{\"weekdays\":[\"monday\",\"THURSDAY\"]}");
        controller.HttpContext.Request.Body = new MemoryStream(payload);
        controller.HttpContext.Request.ContentLength = payload.Length;
        controller.HttpContext.Request.ContentType = "application/json";

        _ = await controller.Simulate(default);

        using var parameters = JsonDocument.Parse(launcher.Request!.ParametersJson);
        Assert.Equal(["MONDAY", "THURSDAY"],
            parameters.RootElement.GetProperty("weekdays").EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    [Fact]
    public async Task Simulate_ReturnsConflictWhenExecutionIsAlreadyActive()
    {
        await using var db = Context();
        var launcher = new FakeLauncher(new ArgumentException(
            "Já existe uma execução deste serviço na fila ou em processamento."));

        var response = await Controller(db, launcher).Simulate(default);

        var conflict = Assert.IsType<ConflictObjectResult>(response);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(conflict.Value));
        Assert.StartsWith("Já existe", payload.RootElement.GetProperty("message").GetString());
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"route-optimization-api-{Guid.NewGuid()}").Options);

    private static RouteOptimizationsController Controller(
        ImportDbContext db,
        IJobExecutionLauncher launcher,
        IImportLifecycleService? importLifecycle = null,
        IBackgroundJobDispatcher? backgroundJobDispatcher = null,
        IMunicipalityCoordinateProvider? municipalityProvider = null) => new(
            db,
            launcher,
            importLifecycle!,
            backgroundJobDispatcher!,
            municipalityProvider!,
            null!,
            null!);

    private static JsonDocument Json(ActionResult? result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    }

    private sealed class FakeLauncher(Exception? exception = null) : IJobExecutionLauncher
    {
        public JobLaunchRequest? Request { get; private set; }
        public Task<JobLaunchResult> LaunchAsync(JobLaunchRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            if (exception is not null) return Task.FromException<JobLaunchResult>(exception);
            return Task.FromResult(new JobLaunchResult(Guid.NewGuid(), "QUEUED"));
        }
    }

    private sealed class FakeLifecycle(ImportDbContext db) : IImportLifecycleService
    {
        public async Task<RouteImport> CreateAsync(Guid dataSourceId, string fileName, string filePath, CancellationToken cancellationToken)
        {
            var version = await db.RouteImports.Where(item => item.DataSourceId == dataSourceId)
                .MaxAsync(item => item.Version, cancellationToken) + 1;
            var item = new RouteImport { Id = Guid.NewGuid(), DataSourceId = dataSourceId, Version = version, FileName = fileName, FilePath = filePath, Status = RouteImportStatus.Queued, CreatedAt = DateTime.UtcNow };
            db.RouteImports.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }

        public Task<bool> TryActivateAsync(Guid importId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class RecordingDispatcher : IBackgroundJobDispatcher
    {
        public Guid? ImportId { get; private set; }
        public string EnqueueImport(Guid importId, Guid jobExecutionId)
        {
            ImportId = importId;
            return "job";
        }
        public string EnqueueOperationalJob(Guid jobExecutionId) => "job";
    }
}
