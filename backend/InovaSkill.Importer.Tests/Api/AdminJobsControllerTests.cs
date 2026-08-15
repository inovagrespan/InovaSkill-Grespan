using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AdminJobsControllerTests
{
    [Fact]
    public async Task RunDefinition_DelegatesValidatedEnvelopeToGenericLauncher()
    {
        await using var dbContext = CreateDbContext();
        var launcher = new CapturingJobExecutionLauncher();
        var controller = new AdminJobsController(dbContext, new NoOpBackgroundJobDispatcher());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        using var parameters = JsonDocument.Parse("""{"importId":"00000000-0000-0000-0000-000000000001","reprocessFailed":false}""");

        var result = await controller.RunDefinition(
            OperationalJobCodes.MunicipalityCoordinateEnrichment,
            new RunJobDefinitionRequest(1, parameters.RootElement), launcher, default);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(OperationalJobCodes.MunicipalityCoordinateEnrichment, launcher.Request?.JobType);
        Assert.Equal(1, launcher.Request?.ContractVersion);
        Assert.Equal(JobExecutionTrigger.Manual, launcher.Request?.Trigger);
    }

    [Fact]
    public async Task RunDefinition_WhenLauncherRejectsParameters_ReturnsBadRequest()
    {
        await using var dbContext = CreateDbContext();
        var launcher = new CapturingJobExecutionLauncher { Failure = new ArgumentException("$.importId é obrigatório.") };
        var controller = new AdminJobsController(dbContext, new NoOpBackgroundJobDispatcher());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        using var parameters = JsonDocument.Parse("{}");

        var result = await controller.RunDefinition(
            OperationalJobCodes.MunicipalityCoordinateEnrichment,
            new RunJobDefinitionRequest(1, parameters.RootElement),
            launcher,
            default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static ImportDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"admin-jobs-{Guid.NewGuid()}")
            .Options);

    private sealed class CapturingJobExecutionLauncher : IJobExecutionLauncher
    {
        public JobLaunchRequest? Request { get; private set; }
        public Exception? Failure { get; init; }

        public Task<JobLaunchResult> LaunchAsync(JobLaunchRequest request, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            Request = request;
            return Task.FromResult(new JobLaunchResult(Guid.NewGuid(), "QUEUED"));
        }
    }

    private sealed class NoOpBackgroundJobDispatcher : IBackgroundJobDispatcher
    {
        public string EnqueueImport(Guid importId, Guid jobExecutionId) => string.Empty;
        public string EnqueueOperationalJob(Guid jobExecutionId) => string.Empty;
    }
}
