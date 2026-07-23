using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AdminJobsControllerTests
{
    [Theory]
    [InlineData(OperationalJobCodes.MunicipalityCoordinateEnrichment, CustomerImportCodes.DataSource)]
    public async Task RunDefinition_QueuesJobWithCurrentImportFromRequiredSource(
        string jobType,
        string sourceCode)
    {
        await using var dbContext = CreateDbContext();
        var currentImportId = Guid.NewGuid();
        dbContext.DataSources.Add(CreateDataSource(sourceCode, currentImportId));
        await dbContext.SaveChangesAsync();
        var queue = new CapturingOperationalJobQueue();
        var controller = new AdminJobsController(dbContext, new NoOpBackgroundJobDispatcher());

        var result = await controller.RunDefinition(jobType, queue, null!, default);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(jobType, queue.JobType);
        Assert.Equal(currentImportId, queue.RelatedEntityId);
    }

    [Fact]
    public async Task RunDefinition_WhenRequiredImportIsNotPublished_ReturnsConflictWithoutQueuing()
    {
        await using var dbContext = CreateDbContext();
        var queue = new CapturingOperationalJobQueue();
        var controller = new AdminJobsController(dbContext, new NoOpBackgroundJobDispatcher());

        var result = await controller.RunDefinition(
            OperationalJobCodes.MunicipalityCoordinateEnrichment,
            queue,
            null!,
            default);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Null(queue.JobType);
        Assert.Null(queue.RelatedEntityId);
    }

    private static ImportDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"admin-jobs-{Guid.NewGuid()}")
            .Options);

    private static DataSource CreateDataSource(string code, Guid currentImportId) => new()
    {
        Id = Guid.NewGuid(), Code = code, Name = code, ProcessorKey = code,
        Type = "EXCEL", CurrentImportId = currentImportId, Active = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class CapturingOperationalJobQueue : IOperationalJobQueue
    {
        public string? JobType { get; private set; }
        public Guid? RelatedEntityId { get; private set; }

        public Task<Guid?> TryQueueAsync(string jobType, Guid relatedEntityId, CancellationToken cancellationToken)
        {
            JobType = jobType;
            RelatedEntityId = relatedEntityId;
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private sealed class NoOpBackgroundJobDispatcher : IBackgroundJobDispatcher
    {
        public string EnqueueImport(Guid importId, Guid jobExecutionId) => string.Empty;
        public string EnqueueOperationalJob(Guid jobExecutionId) => string.Empty;
    }
}
