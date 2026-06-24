using Hangfire;
using InovaSkill.Importer.Application.Analytics.Forecast;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class ForecastWorker(GerarForecastUseCase useCase)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60])]
    [Queue("analytics")]
    public async Task ExecutarAsync(CancellationToken ct)
    {
        await useCase.ExecutarAsync(ct);
    }
}
