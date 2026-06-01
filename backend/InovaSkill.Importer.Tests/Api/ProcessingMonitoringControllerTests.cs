using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class ProcessingMonitoringControllerTests
{
    [Fact]
    public async Task GetDashboard_ReturnsOperationalSummaryFromExistingJobsAndQueue()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.FileJobs.AddRange(
            new FileJob
            {
                Id = 1,
                FilePath = "vendas.xlsx",
                OriginalFileName = "vendas.xlsx",
                ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
                Status = FileJobStatus.Importing,
                CurrentStep = "Importando dados",
                ProgressPercent = 50,
                ProcessedRows = 500,
                TotalRows = 1000,
                CreatedAt = now.AddMinutes(-20),
                StartedAt = now.AddMinutes(-10),
                LastHeartbeatAt = now
            },
            new FileJob
            {
                Id = 2,
                FilePath = "clientes.csv",
                OriginalFileName = "clientes.csv",
                ImportFileTypeCode = ImportFileTypeCodes.CustomerList,
                Status = FileJobStatus.Completed,
                CurrentStep = "Processamento concluido",
                ProgressPercent = 100,
                ProcessedRows = 120,
                TotalRows = 120,
                CreatedAt = now.AddHours(-1),
                StartedAt = now.AddMinutes(-50),
                FinishedAt = now.AddMinutes(-45),
                LastHeartbeatAt = now.AddMinutes(-45)
            });
        db.ImportErrors.Add(new ImportError
        {
            FileJobId = 1,
            Stage = ImportProcessingStages.Import,
            RowNumber = 10,
            Column = "totalamount",
            Message = "Valor invalido.",
            RecordIdentifier = "NF-1"
        });
        db.ProcessingStepExecutions.Add(new ProcessingStepExecution
        {
            FileJobId = 2,
            Step = ImportProcessingStages.Import,
            StartedAt = now.AddMinutes(-48),
            FinishedAt = now.AddMinutes(-45),
            Status = "completed",
            ProcessedRows = 120
        });
        db.WorkerHeartbeats.Add(new WorkerHeartbeat
        {
            WorkerId = "worker-1",
            LastSeenAt = now,
            ProcessedJobsToday = 3,
            CurrentTask = "Aguardando job"
        });
        await db.SaveChangesAsync();

        var controller = new ProcessingMonitoringController(db, new StubQueueMonitor(2, 1), new StubProcessingEventPublisher());

        var result = await controller.GetDashboard();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProcessingMonitoringDashboardDto>(ok.Value);
        Assert.Equal(1, payload.Summary.RunningJobs);
        Assert.Equal(3, payload.Summary.QueuedJobs);
        Assert.Equal(1, payload.Summary.CompletedToday);
        Assert.Equal(620, payload.Summary.ProcessedRowsToday);
        Assert.Contains(payload.Jobs, job => job.Id == 1 && job.ErrorCount == 1);
        Assert.Contains(payload.StageDurations, stage => stage.Stage == ImportProcessingStages.Import);
        Assert.Single(payload.Workers);
    }

    [Fact]
    public async Task GetJobDetails_ReturnsTimelineMetricsAndLogs()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.FileJobs.Add(new FileJob
        {
            Id = 5,
            FilePath = "vendas.xlsx",
            OriginalFileName = "vendas.xlsx",
            ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
            Status = FileJobStatus.Completed,
            CurrentStep = "Processamento concluido",
            ProgressPercent = 100,
            ProcessedRows = 10,
            TotalRows = 12,
            CreatedAt = now.AddMinutes(-10),
            StartedAt = now.AddMinutes(-9),
            FinishedAt = now.AddMinutes(-1),
            LastHeartbeatAt = now.AddMinutes(-1)
        });
        db.ImportErrors.Add(new ImportError
        {
            FileJobId = 5,
            Stage = ImportProcessingStages.Validation,
            RowNumber = 2,
            Column = "data",
            Message = "Data invalida.",
            RecordIdentifier = "NF-1"
        });
        db.ProcessingStepExecutions.Add(new ProcessingStepExecution
        {
            FileJobId = 5,
            Step = ImportProcessingStages.Validation,
            StartedAt = now.AddMinutes(-8),
            FinishedAt = now.AddMinutes(-7),
            Status = "completed",
            ProcessedRows = 12,
            ErrorCount = 1
        });
        db.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = 5,
            Stage = ImportProcessingStages.Validation,
            Level = "Warning",
            Message = "Validacao com erro."
        });
        await db.SaveChangesAsync();

        var controller = new ProcessingMonitoringController(db, new StubQueueMonitor(), new StubProcessingEventPublisher());

        var result = await controller.GetJobDetails(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProcessingJobDetailsDto>(ok.Value);
        Assert.Equal(12, payload.Metrics.TotalRows);
        Assert.Equal(1, payload.Metrics.ErrorCount);
        Assert.Contains(payload.Timeline, step => step.Step == ImportProcessingStages.Validation && step.ErrorCount == 1);
        Assert.Single(payload.Logs);
    }

    [Fact]
    public async Task Cancel_MarksQueuedJobAsCancelled()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 9,
            FilePath = "clientes.csv",
            OriginalFileName = "clientes.csv",
            Status = FileJobStatus.WaitingProcessing,
            CurrentStep = "Aguardando processamento"
        });
        await db.SaveChangesAsync();
        var controller = new ProcessingMonitoringController(db, new StubQueueMonitor(), new StubProcessingEventPublisher());

        var result = await controller.Cancel(9);

        Assert.IsType<OkResult>(result);
        var job = await db.FileJobs.SingleAsync(x => x.Id == 9);
        Assert.Equal(FileJobStatus.Cancelled, job.Status);
        Assert.NotNull(job.FinishedAt);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"processing-monitoring-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private sealed class StubQueueMonitor(int importQueueLength = 0, int postImportQueueLength = 0) : IProcessingQueueMonitor
    {
        public Task<ProcessingQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProcessingQueueSnapshot(importQueueLength, postImportQueueLength));
        }
    }

    private sealed class StubProcessingEventPublisher : IProcessingEventPublisher
    {
        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
