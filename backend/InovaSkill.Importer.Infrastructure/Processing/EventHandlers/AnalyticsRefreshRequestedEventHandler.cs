using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing.EventHandlers;

public sealed class AnalyticsRefreshRequestedEventHandler(ImportDbContext dbContext) : IProcessingEventHandler
{
    public string EventType => ProcessingEventTypes.AnalyticsRefreshRequested;

    public async Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = envelope.JobId,
            Stage = "ANALYTICS",
            Level = "Information",
            Message = "Atualizacao de analytics registrada."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
