using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/analytics/ranking")]
public sealed class RankingClientesController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Ranking(
        CancellationToken ct,
        [FromQuery] string classificacao = "",
        [FromQuery] string tendencia = "",
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50)
    {
        var query = dbContext.ClienteIndicadores.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(classificacao))
            query = query.Where(i => i.Classificacao == classificacao.Trim().ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(tendencia))
            query = query.Where(i => i.Tendencia == tendencia.Trim());

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.ScorePotencial)
            .Skip((Math.Max(1, pagina) - 1) * Math.Clamp(tamanho, 10, 200))
            .Take(Math.Clamp(tamanho, 10, 200))
            .Select(i => new
            {
                i.ClienteId,
                i.Faturamento12M,
                i.Faturamento6M,
                i.Faturamento3M,
                i.Crescimento12M,
                i.Crescimento6M,
                i.Crescimento3M,
                i.FrequenciaCompra,
                i.TicketMedioGeral,
                i.ScorePotencial,
                i.ScoreCrescimento,
                i.ScoreFrequencia,
                i.ScoreTicket,
                i.ScoreRecencia,
                i.Tendencia,
                i.Classificacao
            })
            .ToListAsync(ct);

        return Ok(new
        {
            pagina,
            tamanho,
            total,
            items
        });
    }
}
