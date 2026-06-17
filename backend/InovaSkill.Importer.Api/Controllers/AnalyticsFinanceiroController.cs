using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/analytics-financeiro")]
public sealed class AnalyticsFinanceiroController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet("impacto")]
    public async Task<ActionResult> Impacto(CancellationToken ct)
    {
        var indicadores = await dbContext.ClienteIndicadores.AsNoTracking().ToListAsync(ct);

        var nomes = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        var nomesClientes = nomes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerName.Trim(), StringComparer.OrdinalIgnoreCase);

        if (indicadores.Count == 0)
            return Ok(new { risco = Array.Empty<object>(), crescimento = Array.Empty<object>(), resumo = new { }, alertas = Array.Empty<object>() });

        var totalFaturamento = indicadores.Sum(i => i.Faturamento12M);

        var clientesRisco = indicadores
            .Where(i => i.Tendencia == "Queda" || i.Classificacao == "D" || i.ScoreRecencia < 40)
            .OrderBy(i => i.Crescimento12M ?? 0)
            .Take(10)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                i.Faturamento12M,
                i.Faturamento6M,
                i.Faturamento3M,
                variacaoPercentual = i.Crescimento12M,
                tendencia = i.Tendencia,
                scoreRisco = CalcularScoreRisco(i),
                nivelRisco = ClassificarRisco(CalcularScoreRisco(i)),
                impactoFinanceiro = Math.Abs(i.Crescimento3M ?? 0) > 0
                    ? Math.Round(Math.Abs(i.Faturamento3M * ((i.Crescimento3M ?? 0) / 100m)), 2)
                    : 0,
                mesesQueda = i.Crescimento6M < 0 && i.Crescimento3M < 0 ? "2+ meses consecutivos" : i.Crescimento3M < 0 ? "1 mês" : "—",
                i.Classificacao,
                i.ScoreRecencia,
                participacao = totalFaturamento > 0 ? Math.Round(i.Faturamento12M / totalFaturamento * 100, 1) : 0
            })
            .ToList();

        var crescimento = indicadores
            .Where(i => i.Tendencia == "Crescimento")
            .OrderByDescending(i => i.Crescimento12M ?? 0)
            .Take(10)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                i.Faturamento12M,
                i.Crescimento12M,
                i.ScorePotencial,
                i.ScoreCrescimento,
                valorGerado = i.Faturamento12M - i.Faturamento6M,
                potencialFuturo = ClassificarPotencial(i.ScorePotencial),
                participacao = totalFaturamento > 0 ? Math.Round(i.Faturamento12M / totalFaturamento * 100, 1) : 0
            })
            .ToList();

        var alertas = new List<object>();

        var inativos = indicadores.Where(i => i.ScoreRecencia < 20).ToList();
        if (inativos.Count > 0)
            alertas.Add(new { tipo = "inativos", severidade = "Crítico", mensagem = $"{inativos.Count} cliente(s) sem compra recente (score de recência < 20).", total = inativos.Count });

        var quedaAlta = indicadores.Where(i => i.Crescimento12M < -15).ToList();
        if (quedaAlta.Count > 0)
            alertas.Add(new { tipo = "queda-abrupta", severidade = "Alto", mensagem = $"{quedaAlta.Count} cliente(s) com queda superior a 15%.", total = quedaAlta.Count });

        var dependencia = indicadores.OrderByDescending(i => i.Faturamento12M).Take(3).ToList();
        var pctTop3 = totalFaturamento > 0 ? Math.Round(dependencia.Sum(i => i.Faturamento12M) / totalFaturamento * 100, 1) : 0;
        if (pctTop3 > 50)
            alertas.Add(new { tipo = "dependencia", severidade = "Alto", mensagem = $"Top 3 clientes representam {pctTop3}% do faturamento — risco de concentração.", total = 3 });

        var perdaAcelerada = indicadores.Where(i => i.Crescimento6M < 0 && i.Crescimento3M < i.Crescimento6M).ToList();
        if (perdaAcelerada.Count > 0)
            alertas.Add(new { tipo = "perda-acelerada", severidade = "Alto", mensagem = $"{perdaAcelerada.Count} cliente(s) com perda acelerada de receita.", total = perdaAcelerada.Count });

        var oportunidades = indicadores
            .Where(i => i.Tendencia == "Crescimento" && i.ScorePotencial >= 60)
            .OrderByDescending(i => i.ScorePotencial)
            .Take(10)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                i.ScorePotencial,
                i.Crescimento12M,
                i.Faturamento12M,
                potencial = ClassificarPotencial(i.ScorePotencial),
                i.TicketMedioGeral,
                i.FrequenciaCompra
            })
            .ToList();

        var maior = indicadores.OrderByDescending(i => i.Faturamento12M).FirstOrDefault();
        var maiorCrescimento = indicadores.Where(i => i.Crescimento12M > 0).OrderByDescending(i => i.Crescimento12M).FirstOrDefault();
        var maiorQueda = indicadores.Where(i => i.Crescimento12M < 0).OrderBy(i => i.Crescimento12M).FirstOrDefault();
        var maisConsistente = indicadores.OrderBy(i => Math.Abs((double)(i.Crescimento12M ?? 0))).FirstOrDefault();
        var maiorPotencial = indicadores.OrderByDescending(i => i.ScorePotencial).FirstOrDefault();

        return Ok(new
        {
            risco = clientesRisco,
            crescimento,
            alertas,
            oportunidades,
            resumo = new
            {
                maiorCliente = maior?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maior.ClienteId) ?? maior.ClienteId : null,
                maiorFaturamento = maior?.Faturamento12M,
                maiorCrescimentoNome = maiorCrescimento?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maiorCrescimento.ClienteId) ?? maiorCrescimento.ClienteId : null,
                maiorCrescimentoPct = maiorCrescimento?.Crescimento12M,
                maiorQuedaNome = maiorQueda?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maiorQueda.ClienteId) ?? maiorQueda.ClienteId : null,
                maiorQuedaPct = maiorQueda?.Crescimento12M,
                consistenteNome = maisConsistente?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maisConsistente.ClienteId) ?? maisConsistente.ClienteId : null,
                maiorPotencialNome = maiorPotencial?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maiorPotencial.ClienteId) ?? maiorPotencial.ClienteId : null,
                maiorPotencialScore = maiorPotencial?.ScorePotencial,
                totalClientes = indicadores.Count
            }
        });
    }

    [HttpGet("projecoes")]
    public async Task<ActionResult> Projecoes(CancellationToken ct)
    {
        var indicadores = await dbContext.ClienteIndicadores.AsNoTracking().ToListAsync(ct);
        var forecasts = await dbContext.ClienteForecasts.AsNoTracking().ToListAsync(ct);
        var clientesComForecast = forecasts.Select(f => f.ClienteId).ToHashSet();

        var nomes = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        var nomesClientes = nomes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerName.Trim(), StringComparer.OrdinalIgnoreCase);

        var proj30 = forecasts.Sum(f => f.Previsao30Dias);
        var proj90 = forecasts.Sum(f => f.Previsao30Dias + f.Previsao60Dias + f.Previsao90Dias);
        var proj180 = proj90 + forecasts.Sum(f => f.Previsao90Dias);
        var proj360 = proj180 * 2;

        var faturamentoAtual = indicadores.Sum(i => i.Faturamento12M) / 12m;

        return Ok(new
        {
            projecoes = new
            {
                faturamentoMensalAtual = Math.Round(faturamentoAtual, 2),
                proximoMes = Math.Round(proj30, 2),
                proximos3Meses = Math.Round(proj90, 2),
                proximos6Meses = Math.Round(proj180, 2),
                proximos12Meses = Math.Round(proj360, 2)
            },
            cenarioOtimista = new { margem = 15, descricao = "Otimista (+15%)" },
            cenarioRealista = new { margem = 0, descricao = "Realista" },
            cenarioConservador = new { margem = -15, descricao = "Conservador (-15%)" },
            tendencias = new[]
            {
                new { label = "Crescimento acelerado", total = indicadores.Count(i => i.Tendencia == "Crescimento" && (i.Crescimento12M ?? 0) > 15), faturamento = indicadores.Where(i => i.Tendencia == "Crescimento" && (i.Crescimento12M ?? 0) > 15).Sum(i => i.Faturamento12M) },
                new { label = "Crescimento saudável", total = indicadores.Count(i => i.Tendencia == "Crescimento" && (i.Crescimento12M ?? 0) <= 15), faturamento = indicadores.Where(i => i.Tendencia == "Crescimento" && (i.Crescimento12M ?? 0) <= 15).Sum(i => i.Faturamento12M) },
                new { label = "Estabilidade", total = indicadores.Count(i => i.Tendencia == "Estavel"), faturamento = indicadores.Where(i => i.Tendencia == "Estavel").Sum(i => i.Faturamento12M) },
                new { label = "Desaceleração", total = indicadores.Count(i => i.Tendencia != "Crescimento" && i.Tendencia != "Queda" && i.Tendencia != "Estavel"), faturamento = 0m },
                new { label = "Risco de retração", total = indicadores.Count(i => i.Tendencia == "Queda"), faturamento = indicadores.Where(i => i.Tendencia == "Queda").Sum(i => i.Faturamento12M) },
            },
            evolucaoClientes = !clientesComForecast.Any()
                ? new List<object>()
                : forecasts.Take(20).Select<Domain.Entities.ClienteForecast, object>(f =>
                {
                    var ind = indicadores.FirstOrDefault(i => i.ClienteId == f.ClienteId);
                    var mediaMensal = ind?.Faturamento3M > 0 ? ind.Faturamento3M / 3 : 0;
                    return new
                    {
                        f.ClienteId,
                        clienteNome = nomesClientes.GetValueOrDefault(f.ClienteId) ?? f.ClienteId,
                        valorAtual = mediaMensal,
                        valorProjetado = f.Previsao30Dias,
                        diferenca = f.Previsao30Dias - mediaMensal,
                        f.TendenciaPrevista,
                        f.ConfiancaModelo
                    };
                }).ToList()
        });
    }

    [HttpGet("historico")]
    public async Task<ActionResult> Historico(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        [FromQuery] string sortBy = "revenue",
        CancellationToken ct = default)
    {
        var indicadores = await dbContext.ClienteIndicadores.AsNoTracking().ToListAsync(ct);
        var totalFaturamento = indicadores.Sum(i => i.Faturamento12M);
        var totalVolume = indicadores.Sum(i => i.Faturamento6M);

        var nomes = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        var nomesClientes = nomes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerName.Trim(), StringComparer.OrdinalIgnoreCase);

        var sorted = sortBy.Trim().ToLowerInvariant() switch
        {
            "growth" => indicadores.OrderByDescending(i => i.Crescimento12M).ThenByDescending(i => i.Faturamento12M),
            "drop" => indicadores.OrderBy(i => i.Crescimento12M).ThenByDescending(i => i.Faturamento12M),
            "ticket" => indicadores.OrderByDescending(i => i.TicketMedioGeral).ThenByDescending(i => i.Faturamento12M),
            "quantity" => indicadores.OrderByDescending(i => i.Faturamento6M).ThenByDescending(i => i.Faturamento12M),
            "score" => indicadores.OrderByDescending(i => i.ScorePotencial).ThenByDescending(i => i.Faturamento12M),
            _ => indicadores.OrderByDescending(i => i.Faturamento12M),
        };

        var query = sorted
            .Skip((Math.Max(1, pagina) - 1) * Math.Clamp(tamanho, 10, 200))
            .Take(Math.Clamp(tamanho, 10, 200))
            .Select(i => new
            {
                ClienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                i.ClienteId,
                i.Faturamento12M,
                i.Faturamento6M,
                i.Faturamento3M,
                i.Crescimento12M,
                i.Crescimento6M,
                i.Crescimento3M,
                i.FrequenciaCompra,
                i.TicketMedioGeral,
                i.MediaMovel3M,
                i.ScorePotencial,
                i.ScoreCrescimento,
                i.ScoreFrequencia,
                i.ScoreTicket,
                i.ScoreRecencia,
                i.Tendencia,
                i.Classificacao,
                participacaoFaturamento = totalFaturamento > 0 ? Math.Round(i.Faturamento12M / totalFaturamento * 100, 1) : 0,
                participacaoVolume = totalVolume > 0 ? Math.Round(i.Faturamento6M / totalVolume * 100, 1) : 0,
                status = ClassificarStatus(i),
                scoreRisco = CalcularScoreRisco(i),
                nivelRisco = ClassificarRisco(CalcularScoreRisco(i)),
                potencialLabel = ClassificarPotencial(i.ScorePotencial)
            })
            .ToList();

        return Ok(new { pagina, tamanho, total = indicadores.Count, items = query });
    }

    private static int CalcularScoreRisco(Domain.Entities.ClienteIndicador i)
    {
        var score = 0;
        if (i.Crescimento12M < 0) score += 25;
        if (i.Crescimento6M < 0) score += 15;
        if (i.Crescimento3M < 0) score += 10;
        if (i.FrequenciaCompra < 1) score += 20;
        if (i.ScoreRecencia < 40) score += 20;
        if (i.ScoreRecencia < 20) score += 10;
        if (i.Classificacao == "D") score += 20;
        if (i.Classificacao == "C") score += 10;
        return Math.Min(100, score);
    }

    private static string ClassificarRisco(int score) => score switch
    {
        <= 25 => "Baixo",
        <= 50 => "Médio",
        <= 75 => "Alto",
        _ => "Crítico"
    };

    private static string ClassificarPotencial(int score) => score switch
    {
        >= 80 => "Alto potencial",
        >= 60 => "Bom potencial",
        >= 40 => "Potencial médio",
        _ => "Baixo potencial"
    };

    private static string ClassificarStatus(Domain.Entities.ClienteIndicador i)
    {
        if (i.Tendencia == "Crescimento" && i.ScorePotencial >= 60) return "🟢 Oportunidade";
        if (i.Tendencia == "Queda" || i.Classificacao == "D") return "🔴 Risco";
        if (i.Classificacao == "C" || i.ScoreRecencia < 40) return "🟡 Atenção";
        return "🔵 Estável";
    }
}
