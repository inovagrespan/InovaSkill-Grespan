using InovaSkill.Importer.Application.Analytics.Forecast;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class ClienteForecastRepository(ImportDbContext dbContext) : IClienteForecastRepository
{
    public async Task SalvarAsync(PrevisaoCliente previsao, CancellationToken ct)
    {
        var entity = new ClienteForecast
        {
            ClienteId = previsao.ClienteId,
            Previsao30Dias = previsao.Previsao30Dias,
            Previsao60Dias = previsao.Previsao60Dias,
            Previsao90Dias = previsao.Previsao90Dias,
            TendenciaPrevista = previsao.TendenciaPrevista,
            ErroMedioHistorico = previsao.ErroMedioHistorico,
            ConfiancaModelo = previsao.ConfiancaModelo,
            UltimaObservacao = previsao.UltimaObservacao,
            AtualizadoEm = DateTime.UtcNow
        };
        dbContext.Set<ClienteForecast>().Add(entity);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task SalvarBatchAsync(List<PrevisaoCliente> previsoes, CancellationToken ct)
    {
        var entities = previsoes.Select(p => new ClienteForecast
        {
            ClienteId = p.ClienteId,
            Previsao30Dias = p.Previsao30Dias,
            Previsao60Dias = p.Previsao60Dias,
            Previsao90Dias = p.Previsao90Dias,
            TendenciaPrevista = p.TendenciaPrevista,
            ErroMedioHistorico = p.ErroMedioHistorico,
            ConfiancaModelo = p.ConfiancaModelo,
            UltimaObservacao = p.UltimaObservacao,
            AtualizadoEm = DateTime.UtcNow
        }).ToList();

        dbContext.Set<ClienteForecast>().AddRange(entities);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task LimparAsync(CancellationToken ct)
    {
        dbContext.Set<ClienteForecast>().RemoveRange(dbContext.Set<ClienteForecast>());
        await dbContext.SaveChangesAsync(ct);
    }
}
