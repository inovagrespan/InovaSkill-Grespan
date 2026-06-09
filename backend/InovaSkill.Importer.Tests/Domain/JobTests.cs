using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Tests.DomainTests;

public sealed class JobTests
{
    [Fact]
    public void Job_ShouldProgressThroughQueuedProcessingAndCompletedStatuses()
    {
        var job = new Job { Type = "SpreadsheetImport", PayloadJson = "{}" };

        job.MarkQueued();
        job.MarkProcessing("Importando planilha");
        job.UpdateProgress("Validando dados", 45);
        job.MarkCompleted("""{"fileJobId":10}""");

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(100, job.ProgressPercent);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.FinishedAt);
        Assert.Equal("""{"fileJobId":10}""", job.ResultJson);
        Assert.Equal(string.Empty, job.Error);
    }

    [Fact]
    public void ScheduleRetry_ShouldRecoverFailedProcessingToQueuedWithRetryCount()
    {
        var job = new Job { Type = "SpreadsheetImport", Status = JobStatus.Processing };

        job.ScheduleRetry("timeout");

        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal("timeout", job.Error);
        Assert.Null(job.FinishedAt);
    }

    [Fact]
    public void UpdateProgress_ShouldClampProgressBetweenZeroAndOneHundred()
    {
        var job = new Job { Type = "SpreadsheetImport" };

        job.UpdateProgress("abaixo", -10);
        Assert.Equal(0, job.ProgressPercent);

        job.UpdateProgress("acima", 150);
        Assert.Equal(100, job.ProgressPercent);
    }
}
