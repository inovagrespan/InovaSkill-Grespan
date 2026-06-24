using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/analytics/tendencias")]
public sealed class TendenciasController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Tendencias(CancellationToken ct)
    {
        var tendencias = await dbContext.ClienteIndicadores
            .AsNoTracking()
            .GroupBy(i => i.Tendencia)
            .Select(g => new
            {
                tendencia = g.Key,
                totalClientes = g.Count(),
                faturamentoTotal = g.Sum(i => i.Faturamento12M),
                scoreMedio = (int?)g.Average(i => i.ScorePotencial)
            })
            .ToListAsync(ct);

        return Ok(tendencias);
    }

    [HttpGet("forecast/{clienteId}")]
    public async Task<ActionResult> ForecastCliente(string clienteId, CancellationToken ct)
    {
        var forecast = await dbContext.ClienteForecasts
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ClienteId == clienteId, ct);

        if (forecast is null)
            return NotFound();

        return Ok(new
        {
            forecast.ClienteId,
            forecast.Previsao30Dias,
            forecast.Previsao60Dias,
            forecast.Previsao90Dias,
            forecast.TendenciaPrevista,
            forecast.ErroMedioHistorico,
            forecast.ConfiancaModelo,
            forecast.UltimaObservacao,
            forecast.AtualizadoEm
        });
    }

    [HttpGet("forecast/resumo")]
    public async Task<ActionResult> ResumoForecast(CancellationToken ct)
    {
        var forecasts = await dbContext.ClienteForecasts
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(new
        {
            totalClientes = forecasts.Count,
            tendenciaCrescimento = forecasts.Count(f => f.TendenciaPrevista == "Crescimento"),
            tendenciaEstavel = forecasts.Count(f => f.TendenciaPrevista == "Estavel"),
            tendenciaQueda = forecasts.Count(f => f.TendenciaPrevista == "Queda"),
            confiancaMedia = forecasts.Count > 0 ? forecasts.Where(f => f.ConfiancaModelo.HasValue).Average(f => f.ConfiancaModelo!.Value) : 0,
            receitaEstimada30Dias = forecasts.Sum(f => f.Previsao30Dias),
            receitaEstimada90Dias = forecasts.Sum(f => f.Previsao90Dias)
        });
    }
}
