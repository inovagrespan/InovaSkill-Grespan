using Hangfire;
using InovaSkill.Importer.Application.Analytics.Indicadores;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class ClienteIndicadoresJob(AtualizarIndicadoresUseCase useCase)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60])]
    [Queue("analytics")]
    public async Task ExecutarAsync(CancellationToken ct)
    {
        await useCase.ExecutarAsync(ct);
    }
}
