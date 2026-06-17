using System.Security.Claims;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/analytics/dashboard")]
public sealed class DashboardAnaliseController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet("resumo")]
    public async Task<ActionResult> Resumo(CancellationToken ct)
    {
        var indicadores = await dbContext.ClienteIndicadores
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(new
        {
            totalClientes = indicadores.Count,
            classificacaoA = indicadores.Count(i => i.Classificacao == "A"),
            classificacaoB = indicadores.Count(i => i.Classificacao == "B"),
            classificacaoC = indicadores.Count(i => i.Classificacao == "C"),
            classificacaoD = indicadores.Count(i => i.Classificacao == "D"),
            tendenciaCrescimento = indicadores.Count(i => i.Tendencia == "Crescimento"),
            tendenciaEstavel = indicadores.Count(i => i.Tendencia == "Estavel"),
            tendenciaQueda = indicadores.Count(i => i.Tendencia == "Queda"),
            scoreMedio = indicadores.Count > 0 ? (int)indicadores.Average(i => i.ScorePotencial) : 0,
            faturamentoTotal12M = indicadores.Sum(i => i.Faturamento12M),
            atualizadoEm = indicadores.FirstOrDefault()?.AtualizadoEm
        });
    }

    [HttpGet("top-clientes")]
    public async Task<ActionResult> TopClientes(
        CancellationToken ct,
        [FromQuery] int limite = 20,
        [FromQuery] string ordem = "score")
    {
        var query = dbContext.ClienteIndicadores.AsNoTracking();

        var nomes = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        var nomesClientes = nomes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerName.Trim(), StringComparer.OrdinalIgnoreCase);

        query = ordem.ToLowerInvariant() switch
        {
            "faturamento" => query.OrderByDescending(i => i.Faturamento12M),
            "crescimento" => query.OrderByDescending(i => i.Crescimento12M ?? 0),
            "ticket" => query.OrderByDescending(i => i.TicketMedioGeral),
            _ => query.OrderByDescending(i => i.ScorePotencial)
        };

        var items = await query
            .Take(Math.Clamp(limite, 1, 200))
            .Select(i => new
            {
                clienteId = i.ClienteId,
                faturamento12M = i.Faturamento12M,
                faturamento6M = i.Faturamento6M,
                faturamento3M = i.Faturamento3M,
                crescimento12M = i.Crescimento12M,
                crescimento6M = i.Crescimento6M,
                crescimento3M = i.Crescimento3M,
                scorePotencial = i.ScorePotencial,
                tendencia = i.Tendencia,
                classificacao = i.Classificacao
            })
            .ToListAsync(ct);

        var itemsComNome = items.Select(i => new
        {
            i.clienteId,
            clienteNome = nomesClientes.GetValueOrDefault(i.clienteId) ?? i.clienteId,
            i.faturamento12M,
            i.faturamento6M,
            i.faturamento3M,
            i.crescimento12M,
            i.crescimento6M,
            i.crescimento3M,
            i.scorePotencial,
            i.tendencia,
            i.classificacao
        }).ToList();

        return Ok(itemsComNome);
    }

    [HttpGet("detalhe/{clienteId}")]
    public async Task<ActionResult> DetalheCliente(string clienteId, CancellationToken ct)
    {
        var indicador = await dbContext.ClienteIndicadores
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ClienteId == clienteId, ct);

        if (indicador is null)
            return NotFound();

        var nome = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerCode == clienteId && x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => x.CustomerName.Trim())
            .FirstOrDefaultAsync(ct);

        var forecast = await dbContext.ClienteForecasts
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ClienteId == clienteId, ct);

        return Ok(new
        {
            indicador = new
            {
                indicador.ClienteId,
                clienteNome = nome ?? indicador.ClienteId,
                indicador.Faturamento3M,
                indicador.Faturamento6M,
                indicador.Faturamento12M,
                indicador.Crescimento3M,
                indicador.Crescimento6M,
                indicador.Crescimento12M,
                indicador.MediaMovel3M,
                indicador.MediaMovel6M,
                indicador.MediaMovel12M,
                indicador.FrequenciaCompra,
                indicador.TicketMedioGeral,
                indicador.ScoreCrescimento,
                indicador.ScoreFrequencia,
                indicador.ScoreTicket,
                indicador.ScoreRecencia,
                indicador.ScorePotencial,
                indicador.Tendencia,
                indicador.Classificacao,
                indicador.AtualizadoEm
            },
            forecast = forecast is null ? null : new
            {
                forecast.Previsao30Dias,
                forecast.Previsao60Dias,
                forecast.Previsao90Dias,
                forecast.TendenciaPrevista,
                forecast.ErroMedioHistorico,
                forecast.ConfiancaModelo
            }
        });
    }
}
