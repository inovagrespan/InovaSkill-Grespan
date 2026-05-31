using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Tests.DomainTests;

public sealed class FileJobTests
{
    [Fact]
    public void RequeueManually_ShouldRestartFromPreProcessingEvenWhenFileTypeWasAlreadyDetected()
    {
        var job = new FileJob
        {
            ImportFileTypeCode = ImportFileTypeCodes.SalesInvoice,
            Status = FileJobStatus.Failed,
            ProgressPercent = 2,
            ProcessedRows = 4800,
            TotalRows = 269183
        };

        job.RequeueManually();

        Assert.Equal(FileJobStatus.WaitingProcessing, job.Status);
        Assert.Equal(0, job.ProgressPercent);
        Assert.Equal(0, job.ProcessedRows);
        Assert.Equal(0, job.TotalRows);
    }
}
