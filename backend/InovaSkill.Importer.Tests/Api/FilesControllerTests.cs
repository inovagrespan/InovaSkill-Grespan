using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class FilesControllerTests
{
    [Fact]
    public async Task GetJobs_ReturnsIndependentStageProgress()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 10,
            FilePath = "clientes.csv",
            OriginalFileName = "clientes.csv",
            Status = FileJobStatus.Validating,
            CurrentStep = "Validando arquivo normalizado",
            ProgressPercent = 55,
            ProcessedRows = 55,
            TotalRows = 100,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new FilesController(new StubUploadService(), new StubJobService(), db);

        var result = await controller.GetJobs();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<FileJobDto>>(ok.Value);
        var job = Assert.Single(payload.Items);

        Assert.Equal(55, job.ProgressPercent);
        Assert.Equal(ImportProcessingStages.Validation, job.CurrentStageCode);
        Assert.Collection(
            job.Stages,
            stage =>
            {
                Assert.Equal(ImportProcessingStages.PreProcessing, stage.Code);
                Assert.Equal("completed", stage.Status);
                Assert.Equal(100, stage.ProgressPercent);
            },
            stage =>
            {
                Assert.Equal(ImportProcessingStages.Validation, stage.Code);
                Assert.Equal("running", stage.Status);
                Assert.Equal(55, stage.ProgressPercent);
            },
            stage =>
            {
                Assert.Equal(ImportProcessingStages.Import, stage.Code);
                Assert.Equal("pending", stage.Status);
                Assert.Equal(0, stage.ProgressPercent);
            });
    }

    [Fact]
    public async Task GetJobs_ReturnsErrorCountByStage()
    {
        await using var db = CreateDb();
        db.FileJobs.Add(new FileJob
        {
            Id = 20,
            FilePath = "vendas.csv",
            OriginalFileName = "vendas.csv",
            Status = FileJobStatus.ValidationFailed,
            CurrentStep = "Validacao com erros",
            ProgressPercent = 100,
            ProcessedRows = 3,
            TotalRows = 3,
            CreatedAt = DateTime.UtcNow
        });
        db.ImportErrors.AddRange(
            new ImportError
            {
                FileJobId = 20,
                Stage = ImportProcessingStages.PreProcessing,
                RowNumber = 2,
                Column = "documento",
                Message = "Mapeamento invalido.",
                RecordIdentifier = "NF1"
            },
            new ImportError
            {
                FileJobId = 20,
                Stage = ImportProcessingStages.Validation,
                RowNumber = 3,
                Column = "data",
                Message = "Data invalida.",
                RecordIdentifier = "NF2"
            });
        await db.SaveChangesAsync();

        var controller = new FilesController(new StubUploadService(), new StubJobService(), db);

        var result = await controller.GetJobs();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<FileJobDto>>(ok.Value);
        var job = Assert.Single(payload.Items);

        Assert.Equal(2, job.ErrorCount);
        Assert.Equal(1, job.Stages.Single(x => x.Code == ImportProcessingStages.PreProcessing).ErrorCount);
        Assert.Equal("failed", job.Stages.Single(x => x.Code == ImportProcessingStages.PreProcessing).Status);
        Assert.Equal(1, job.Stages.Single(x => x.Code == ImportProcessingStages.Validation).ErrorCount);
        Assert.Equal("failed", job.Stages.Single(x => x.Code == ImportProcessingStages.Validation).Status);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"files-controller-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private sealed class StubUploadService : IFileUploadService
    {
        public Task<long> UploadAndCreateJobAsync(Stream stream, string originalFileName, string? importFileTypeCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(1L);
        }
    }

    private sealed class StubJobService : IJobService
    {
        public Task<long> EnqueueAsync(string type, object payload, string? userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(1L);
        }
    }
}
