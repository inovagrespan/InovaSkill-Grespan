using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.Extensions.Configuration;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class FileUploadServiceTests
{
    [Fact]
    public async Task UploadAndCreateJobAsync_CreatesJobAndPublishesFileUploadedEvent()
    {
        var uploadsPath = Path.Combine(Path.GetTempPath(), $"upload-events-{Guid.NewGuid():N}");
        var jobService = new StubFileJobService();
        var publisher = new RecordingPublisher();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadsPath"] = uploadsPath })
            .Build();
        var service = new FileUploadService(jobService, publisher, configuration);

        try
        {
            await using var stream = new MemoryStream("a,b\n1,2"u8.ToArray());

            var jobId = await service.UploadAndCreateJobAsync(stream, "clientes.csv", "CUSTOMER_LIST", CancellationToken.None);

            Assert.Equal(77, jobId);
            Assert.Single(jobService.CreatedJobs);
            var published = Assert.Single(publisher.Published);
            Assert.Equal(ProcessingEventTypes.FileUploaded, published.EventType);
            Assert.Equal(77, published.JobId);
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

    private sealed class RecordingPublisher : IProcessingEventPublisher
    {
        public List<ProcessingEventEnvelope> Published { get; } = [];

        public Task PublishAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
