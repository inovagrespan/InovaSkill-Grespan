using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class ProcessingMonitoringControllerTests
{
    [Fact]
    public async Task GetDashboard_ReturnsOperationalSummaryFromExistingJobsAndQueue()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow.Date.AddHours(12);
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
                ImportFileTypeCode = ImportFileTypeCodes.Customers,
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

        var controller = CreateController(db, new StubQueueMonitor(2, 1), new StubJobService(), new StubPostImportJobQueue());

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

        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), new StubPostImportJobQueue());

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
        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), new StubPostImportJobQueue());

        var result = await controller.Cancel(9);

        Assert.IsType<OkResult>(result);
        var job = await db.FileJobs.SingleAsync(x => x.Id == 9);
        Assert.Equal(FileJobStatus.Cancelled, job.Status);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task GetDashboard_ClampsInvalidProgressAndFlagsStaleWorkers()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.FileJobs.Add(new FileJob
        {
            Id = 11,
            FilePath = "vendas.xlsx",
            OriginalFileName = "vendas.xlsx",
            ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
            Status = FileJobStatus.Importing,
            CurrentStep = "Importando dados",
            ProgressPercent = 140,
            ProcessedRows = 10,
            TotalRows = 100,
            CreatedAt = now.AddMinutes(-15),
            StartedAt = now.AddMinutes(-10),
            LastHeartbeatAt = now.AddMinutes(-10)
        });
        db.WorkerHeartbeats.Add(new WorkerHeartbeat
        {
            WorkerId = "worker-offline",
            LastSeenAt = now.AddMinutes(-2),
            ProcessedJobsToday = 1,
            CurrentTask = "Importando dados"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), new StubPostImportJobQueue());

        var result = await controller.GetDashboard();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProcessingMonitoringDashboardDto>(ok.Value);
        Assert.Equal(1, payload.Summary.StaleJobs);
        Assert.Contains(payload.Jobs, job => job.Id == 11 && job.ProgressPercent == 100);
        Assert.Contains(payload.Workers, worker => worker.WorkerId == "worker-offline" && worker.Status == "Offline");
    }

    [Fact]
    public async Task Cancel_RejectsCompletedJobWithoutChangingItsTerminalState()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 12,
            FilePath = "vendas.xlsx",
            OriginalFileName = "vendas.xlsx",
            Status = FileJobStatus.Completed,
            CurrentStep = "Processamento concluido",
            FinishedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), new StubPostImportJobQueue());

        var result = await controller.Cancel(12);

        Assert.IsType<ConflictObjectResult>(result);
        var job = await db.FileJobs.SingleAsync(x => x.Id == 12);
        Assert.Equal(FileJobStatus.Completed, job.Status);
    }

    [Fact]
    public async Task RunManualAction_EnqueuesPostImportJobForCompletedFileJobs()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 20,
            FilePath = "vendas.xlsx",
            OriginalFileName = "vendas.xlsx",
            Status = FileJobStatus.Completed,
            CurrentStep = "Processamento concluido",
            FinishedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var queue = new StubPostImportJobQueue();
        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), queue);

        var result = await controller.RunManualAction(new ProcessingManualActionRequestDto("sales-summary", [20]));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProcessingManualActionResponseDto>(ok.Value);
        Assert.Equal("sales-summary", payload.Action);
        Assert.Equal(1, payload.EnqueuedJobs);
        var queued = Assert.Single(queue.Items);
        Assert.Equal(20, queued.FileJobId);
        Assert.Equal(PostImportJobType.SalesSummary, queued.JobType);
    }

    [Fact]
    public async Task RunManualAction_RejectsNonCompletedJobs()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 21,
            FilePath = "clientes.xlsx",
            OriginalFileName = "clientes.xlsx",
            Status = FileJobStatus.Importing,
            CurrentStep = "Importando dados"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new StubQueueMonitor(), new StubJobService(), new StubPostImportJobQueue());

        var result = await controller.RunManualAction(new ProcessingManualActionRequestDto("customer-summary", [21]));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDashboard_ReturnsForbiddenForGestor()
    {
        await using var db = CreateDb();
        var controller = CreateController(
            db,
            new StubQueueMonitor(),
            new StubJobService(),
            new StubPostImportJobQueue(),
            AppUserRoles.Gestor);

        var result = await controller.GetDashboard();

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"processing-monitoring-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private static ProcessingMonitoringController CreateController(
        ImportDbContext db,
        IProcessingQueueMonitor queueMonitor,
        IJobService jobService,
        IPostImportJobQueue postImportJobQueue,
        string role = AppUserRoles.Admin)
    {
        var controller = new ProcessingMonitoringController(db, queueMonitor, jobService, postImportJobQueue);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("role", role),
                    new Claim(ClaimTypes.Role, role)
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private sealed class StubQueueMonitor(int importQueueLength = 0, int postImportQueueLength = 0) : IProcessingQueueMonitor
    {
        public Task<ProcessingQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProcessingQueueSnapshot(importQueueLength, postImportQueueLength));
        }
    }

    private sealed class StubJobService : IJobService
    {
        public Task<long> EnqueueAsync(string type, object payload, string? userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(1L);
        }
    }

    private sealed class StubPostImportJobQueue : IPostImportJobQueue
    {
        public List<PostImportJobItem> Items { get; } = [];

        public Task EnqueueAsync(PostImportJobItem job, CancellationToken cancellationToken)
        {
            Items.Add(job);
            return Task.CompletedTask;
        }

        public Task<PostImportJobItem?> DequeueAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<PostImportJobItem?>(null);
        }
    }
}
