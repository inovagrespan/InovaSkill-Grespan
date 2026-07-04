using Hangfire;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class RefreshMetricasJob(ImportDbContext dbContext)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60])]
    [Queue("analytics")]
    public async Task ExecutarAsync(CancellationToken ct)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "REFRESH MATERIALIZED VIEW CONCURRENTLY mv_cliente_metricas_mensais", ct);
    }
}
