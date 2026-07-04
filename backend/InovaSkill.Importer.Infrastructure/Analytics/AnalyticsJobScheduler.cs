using Hangfire;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class AnalyticsJobScheduler(IRecurringJobManager recurringJobs)
{
    public void AgendarJobsRecorrentes()
    {
        recurringJobs.AddOrUpdate<RefreshMetricasJob>(
            "analytics-refresh-mv",
            j => j.ExecutarAsync(CancellationToken.None),
            "0 2 * * *");

        recurringJobs.AddOrUpdate<ClienteIndicadoresJob>(
            "analytics-calcular-indicadores",
            j => j.ExecutarAsync(CancellationToken.None),
            "15 2 * * *");

        recurringJobs.AddOrUpdate<ForecastWorker>(
            "analytics-gerar-forecast",
            j => j.ExecutarAsync(CancellationToken.None),
            "0 3 * * *");
    }
}
