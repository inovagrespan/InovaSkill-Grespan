using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Jobs;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.Extensions.Configuration;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class FileUploadServiceTests
{
    [Fact]
    public async Task UploadAndCreateJobAsync_CreatesFileJobAndEnqueuesGenericImportJob()
    {
        var uploadsPath = Path.Combine(Path.GetTempPath(), $"upload-events-{Guid.NewGuid():N}");
        var jobService = new StubFileJobService();
        var genericJobService = new RecordingJobService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadsPath"] = uploadsPath })
            .Build();
        var service = new FileUploadService(jobService, genericJobService, configuration);

        try
        {
            await using var stream = new MemoryStream("a,b\n1,2"u8.ToArray());

            var jobId = await service.UploadAndCreateJobAsync(stream, "clientes.csv", ImportFileTypeCodes.Customers, CancellationToken.None);

            Assert.Equal(77, jobId);
            Assert.Single(jobService.CreatedJobs);
            var enqueued = Assert.Single(genericJobService.Enqueued);
            Assert.Equal(JobTypeCodes.SpreadsheetImport, enqueued.Type);
            var payload = Assert.IsType<SpreadsheetImportJobPayload>(enqueued.Payload);
            Assert.Equal(77, payload.FileJobId);
            Assert.True(File.Exists(jobService.CreatedJobs[0].FilePath));
        }
        finally
        {
            if (Directory.Exists(uploadsPath))
            {
                Directory.Delete(uploadsPath, recursive: true);
            }
        }
    }

    private sealed class StubFileJobService : IFileJobService
    {
        public List<(string FilePath, string OriginalFileName, string? ImportFileTypeCode)> CreatedJobs { get; } = [];

        public Task<long> CreateFileJobAsync(string filePath, string originalFileName, string? importFileTypeCode, CancellationToken cancellationToken)
        {
            CreatedJobs.Add((filePath, originalFileName, importFileTypeCode));
            return Task.FromResult(77L);
        }
    }

    private sealed class RecordingJobService : IJobService
    {
        public List<(string Type, object Payload, string? UserId)> Enqueued { get; } = [];

        public Task<long> EnqueueAsync(string type, object payload, string? userId, CancellationToken cancellationToken)
        {
            Enqueued.Add((type, payload, userId));
            return Task.FromResult(500L);
        }
    }
}
