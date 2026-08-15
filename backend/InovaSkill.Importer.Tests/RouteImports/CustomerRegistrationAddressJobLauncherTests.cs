using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerRegistrationAddressJobLauncherTests
{
    [Fact]
    public async Task LaunchAsync_UsesCurrentCustomerSnapshotWhenImportIdIsOmitted()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"customer-address-launcher-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var importId = Guid.NewGuid();
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            CurrentImportId = importId, NextImportVersion = 2, Active = true,
            CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = importId, DataSourceId = source.Id, DataSource = source, Version = 1,
            FileName = "clientes.xlsx", FilePath = "clientes.xlsx",
            Status = RouteImportStatus.Completed, CreatedAt = now
        };
        db.AddRange(source, import);
        await db.SaveChangesAsync();
        var dispatcher = new RecordingDispatcher();
        var launcher = new JobExecutionLauncher(db, dispatcher);

        var result = await launcher.LaunchAsync(new JobLaunchRequest(
            OperationalJobCodes.CustomerRegistrationAddressEnrichment,
            1,
            "{}",
            JobExecutionTrigger.Manual,
            1), CancellationToken.None);

        var job = await db.JobExecutions.SingleAsync(item => item.Id == result.JobExecutionId);
        Assert.Equal(importId, job.RelatedEntityId);
        Assert.Equal(job.Id, dispatcher.OperationalJobId);
    }

    private sealed class RecordingDispatcher : IBackgroundJobDispatcher
    {
        public Guid? OperationalJobId { get; private set; }
        public string EnqueueImport(Guid importId, Guid jobExecutionId) => jobExecutionId.ToString();

        public string EnqueueOperationalJob(Guid jobExecutionId)
        {
            OperationalJobId = jobExecutionId;
            return jobExecutionId.ToString();
        }
    }
}
