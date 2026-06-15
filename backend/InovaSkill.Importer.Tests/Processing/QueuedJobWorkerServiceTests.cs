using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Worker;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class QueuedJobWorkerServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesQueuedJobFromDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<ImportDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IJobHandler, RecordingJobHandler>();
        await using var provider = services.BuildServiceProvider();

        long jobId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            var queuedJob = new Job
            {
                Type = RecordingJobHandler.HandledJobType,
                Status = JobStatus.Queued,
                PayloadJson = "{}",
                CurrentStep = "Enfileirado"
            };
            dbContext.Jobs.Add(queuedJob);
            await dbContext.SaveChangesAsync();
            jobId = queuedJob.Id;
        }

        var worker = new QueuedJobWorkerService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<QueuedJobWorkerService>.Instance);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(4));

        await worker.StartAsync(cancellationSource.Token);
        await Task.Delay(1000, cancellationSource.Token);
        await worker.StopAsync(CancellationToken.None);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ImportDbContext>();
        var job = await verificationDb.Jobs.SingleAsync(x => x.Id == jobId);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(100, job.ProgressPercent);
        Assert.Equal("Processamento concluido", job.CurrentStep);
        Assert.Equal(string.Empty, job.LockedBy);
        Assert.Null(job.LockedAt);
    }

    private sealed class RecordingJobHandler : IJobHandler
    {
        public const string HandledJobType = "SpreadsheetImport";

        public string JobType => HandledJobType;

        public Task HandleAsync(Job job, CancellationToken cancellationToken)
        {
            job.UpdateProgress("Executando teste", 55);
            return Task.CompletedTask;
        }
    }
}
