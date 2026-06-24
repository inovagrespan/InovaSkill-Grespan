using InovaSkill.Importer.Application.Analytics.Indicadores;

namespace InovaSkill.Importer.Application.Analytics.Forecast;

public sealed record PrevisaoCliente(
    string ClienteId,
    decimal Previsao30Dias,
    decimal Previsao60Dias,
    decimal Previsao90Dias,
    string TendenciaPrevista,
    decimal? ErroMedioHistorico,
    decimal? ConfiancaModelo,
    DateTime UltimaObservacao);

public interface IClienteForecastRepository
{
    Task SalvarAsync(PrevisaoCliente previsao, CancellationToken ct);
    Task SalvarBatchAsync(List<PrevisaoCliente> previsoes, CancellationToken ct);
    Task LimparAsync(CancellationToken ct);
}

public sealed class GerarForecastUseCase(
    IClienteMetricaMensalRepository metricaRepo,
    IClienteForecastRepository forecastRepo)
{
    public async Task ExecutarAsync(CancellationToken ct)
    {
        var clientesAtivos = await metricaRepo.ObterClientesAtivosAsync(ct);
        if (clientesAtivos.Count == 0) return;

        var metricas = await metricaRepo.ObterTodasAsync(ct);
        var agrupado = metricas
            .GroupBy(m => m.ClienteId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.AnoMes).Select(m => m.FaturamentoTotal).ToList());

        var previsoes = new List<PrevisaoCliente>();

        foreach (var clienteId in clientesAtivos)
        {
            if (!agrupado.TryGetValue(clienteId, out var serie))
                continue;

            if (serie.Count < 3)
                continue;

            var previsao = Calcular(clienteId, serie);
            previsoes.Add(previsao);
        }

        await forecastRepo.LimparAsync(ct);
        await forecastRepo.SalvarBatchAsync(previsoes, ct);
    }

    private static PrevisaoCliente Calcular(string clienteId, List<decimal> serie)
    {
        var holtWinters = new HoltWinters(serie);
        holtWinters.Ajustar();

        var passos30 = Math.Min(30, Math.Max(12, serie.Count));
        var passos60 = Math.Min(60, Math.Max(12, serie.Count));
        var passos90 = Math.Min(90, Math.Max(12, serie.Count));

        var previsao30 = Math.Max(0, holtWinters.Prever(passos30));
        var previsao60 = Math.Max(0, holtWinters.Prever(passos60));
        var previsao90 = Math.Max(0, holtWinters.Prever(passos90));

        var tendencia = CalcularTendenciaPrevista(previsao30, serie.LastOrDefault());
        var confianca = CalcularConfianca(serie, holtWinters);
        var erroMedio = CalcularErroMedio(serie, holtWinters);
        var ultimaObservacao = DateTime.UtcNow;

        return new PrevisaoCliente(
            clienteId,
            previsao30,
            previsao60,
            previsao90,
            tendencia,
            erroMedio,
            confianca,
            ultimaObservacao);
    }

    private static string CalcularTendenciaPrevista(decimal previsao, decimal ultimo)
    {
        if (ultimo == 0) return previsao > 0 ? "Crescimento" : "Estavel";
        var variacao = (previsao - ultimo) / ultimo;
        return variacao switch
        {
            > 0.1m => "Crescimento",
            < -0.1m => "Queda",
            _ => "Estavel"
        };
    }

    private static decimal? CalcularConfianca(List<decimal> serie, HoltWinters hw)
    {
        if (serie.Count < 3) return null;
        var mse = serie.Select((v, i) => Math.Pow((double)(v - hw.Suavizados[i]), 2)).Average();
        var rmse = Math.Sqrt(mse);
        var media = (double)serie.Average();
        if (media == 0) return null;
        var cv = rmse / media;
        var confianca = Math.Max(0, Math.Min(100, (1 - cv) * 100));
        return Math.Round((decimal)confianca, 1);
    }

    private static decimal? CalcularErroMedio(List<decimal> serie, HoltWinters hw)
    {
        if (serie.Count < 2) return null;
        var erros = serie.Select((v, i) => Math.Abs((double)(v - hw.Suavizados[i]))).ToList();
        var mae = erros.Average();
        return Math.Round((decimal)mae, 2);
    }
}
