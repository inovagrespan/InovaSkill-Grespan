using InovaSkill.Importer.Application.Analytics.Indicadores;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Analytics;

public sealed class ClienteMetricaMensalRepository(ImportDbContext dbContext) : IClienteMetricaMensalRepository
{
    public async Task<List<ClienteMetricaMensal>> ObterTodasAsync(CancellationToken ct)
    {
        var sql = $@"
            SELECT
                cliente_id AS {nameof(ClienteMetricaMensal.ClienteId)},
                ano_mes AS {nameof(ClienteMetricaMensal.AnoMes)},
                faturamento_total AS {nameof(ClienteMetricaMensal.FaturamentoTotal)},
                quantidade_notas AS {nameof(ClienteMetricaMensal.QuantidadeNotas)},
                ticket_medio AS {nameof(ClienteMetricaMensal.TicketMedio)},
                primeiro_faturamento_mes AS {nameof(ClienteMetricaMensal.PrimeiroFaturamentoMes)},
                ultimo_faturamento_mes AS {nameof(ClienteMetricaMensal.UltimoFaturamentoMes)}
            FROM mv_cliente_metricas_mensais";
        return await dbContext.Database
            .SqlQueryRaw<ClienteMetricaMensal>(sql)
            .ToListAsync(ct);
    }

    public async Task<List<string>> ObterClientesAtivosAsync(CancellationToken ct)
    {
        return await dbContext.Database
            .SqlQueryRaw<string>(@"
                SELECT DISTINCT cliente_id
                FROM mv_cliente_metricas_mensais
                WHERE ultimo_faturamento_mes >= NOW() - INTERVAL '90 days'")
            .ToListAsync(ct);
    }
}
