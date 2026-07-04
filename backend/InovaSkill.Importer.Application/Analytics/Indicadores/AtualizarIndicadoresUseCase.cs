using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Analytics.Indicadores;

public sealed record ClienteMetricaMensal(
    string ClienteId,
    string AnoMes,
    decimal FaturamentoTotal,
    int QuantidadeNotas,
    decimal TicketMedio,
    DateTime PrimeiroFaturamentoMes,
    DateTime UltimoFaturamentoMes);

public sealed record ClienteIndicadorDto(
    string ClienteId,
    decimal Faturamento3M,
    decimal Faturamento6M,
    decimal Faturamento12M,
    decimal? Crescimento3M,
    decimal? Crescimento6M,
    decimal? Crescimento12M,
    decimal MediaMovel3M,
    decimal MediaMovel6M,
    decimal MediaMovel12M,
    decimal FrequenciaCompra,
    decimal TicketMedioGeral,
    int ScoreCrescimento,
    int ScoreFrequencia,
    int ScoreTicket,
    int ScoreRecencia,
    int ScorePotencial,
    string Tendencia,
    string Classificacao);

public interface IClienteMetricaMensalRepository
{
    Task<List<ClienteMetricaMensal>> ObterTodasAsync(CancellationToken ct);
    Task<List<string>> ObterClientesAtivosAsync(CancellationToken ct);
}

public interface IClienteIndicadorRepository
{
    Task SalvarAsync(ClienteIndicador indicador, CancellationToken ct);
    Task SalvarBatchAsync(List<ClienteIndicador> indicadores, CancellationToken ct);
    Task LimparAsync(CancellationToken ct);
}

public sealed class AtualizarIndicadoresUseCase(
    IClienteMetricaMensalRepository metricaRepo,
    IClienteIndicadorRepository indicadorRepo)
{
    private const int LimiteInatividadeDias = 90;

    public async Task ExecutarAsync(CancellationToken ct)
    {
        var metricas = await metricaRepo.ObterTodasAsync(ct);
        if (metricas.Count == 0) return;

        var agrupado = metricas
            .GroupBy(m => m.ClienteId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.AnoMes).ToList());

        var indicadores = new List<ClienteIndicador>();
        var agora = DateTime.UtcNow;

        foreach (var (clienteId, meses) in agrupado)
        {
            var indicador = Calcular(clienteId, meses, agora);
            indicadores.Add(indicador);
        }

        await indicadorRepo.LimparAsync(ct);
        await indicadorRepo.SalvarBatchAsync(indicadores, ct);
    }

    private static ClienteIndicador Calcular(string clienteId, List<ClienteMetricaMensal> meses, DateTime agora)
    {
        var mesesOrdenados = meses.OrderByDescending(m => m.AnoMes).ToList();

        var fat3M = FiltrarPorMeses(mesesOrdenados, 3).Sum(m => m.FaturamentoTotal);
        var fat6M = FiltrarPorMeses(mesesOrdenados, 6).Sum(m => m.FaturamentoTotal);
        var fat12M = FiltrarPorMeses(mesesOrdenados, 12).Sum(m => m.FaturamentoTotal);

        var fatAnterior3M = FiltrarPorMesesOffset(mesesOrdenados, 3, 3).Sum(m => m.FaturamentoTotal);
        var fatAnterior6M = FiltrarPorMesesOffset(mesesOrdenados, 6, 6).Sum(m => m.FaturamentoTotal);
        var fatAnterior12M = FiltrarPorMesesOffset(mesesOrdenados, 12, 12).Sum(m => m.FaturamentoTotal);

        var ultimoMes = mesesOrdenados.FirstOrDefault();

        var ticketMedioGeral = CalcularTicketMedioGeral(mesesOrdenados);

        var frequenciaCompra = CalcularFrequenciaCompra(mesesOrdenados, ultimoMes?.UltimoFaturamentoMes, agora);

        var scoreCrescimento = CalcularScoreCrescimento(fat3M, fatAnterior3M);
        var scoreFrequencia = CalcularScoreFrequencia(frequenciaCompra);
        var scoreTicket = CalcularScoreTicket(ticketMedioGeral);
        var scoreRecencia = CalcularScoreRecencia(ultimoMes?.UltimoFaturamentoMes, agora);

        var scorePotencial = CalcularScorePotencial(scoreCrescimento, scoreFrequencia, scoreTicket, scoreRecencia);

        return new ClienteIndicador
        {
            ClienteId = clienteId,
            Faturamento3M = fat3M,
            Faturamento6M = fat6M,
            Faturamento12M = fat12M,
            Crescimento3M = CalcularVariacao(fat3M, fatAnterior3M),
            Crescimento6M = CalcularVariacao(fat6M, fatAnterior6M),
            Crescimento12M = CalcularVariacao(fat12M, fatAnterior12M),
            MediaMovel3M = mesesOrdenados.Take(3).Average(m => m.FaturamentoTotal),
            MediaMovel6M = mesesOrdenados.Take(6).Average(m => m.FaturamentoTotal),
            MediaMovel12M = mesesOrdenados.Take(12).Average(m => m.FaturamentoTotal),
            FrequenciaCompra = frequenciaCompra,
            TicketMedioGeral = ticketMedioGeral,
            ScoreCrescimento = scoreCrescimento,
            ScoreFrequencia = scoreFrequencia,
            ScoreTicket = scoreTicket,
            ScoreRecencia = scoreRecencia,
            ScorePotencial = scorePotencial,
            Tendencia = ClassificarTendencia(mesesOrdenados),
            Classificacao = Classificar(scorePotencial),
            AtualizadoEm = DateTime.UtcNow
        };
    }

    private static List<ClienteMetricaMensal> FiltrarPorMeses(List<ClienteMetricaMensal> ordenados, int qtd)
    {
        return ordenados.Take(qtd).ToList();
    }

    private static List<ClienteMetricaMensal> FiltrarPorMesesOffset(List<ClienteMetricaMensal> ordenados, int qtd, int offset)
    {
        return ordenados.Skip(offset).Take(qtd).ToList();
    }

    private static decimal? CalcularVariacao(decimal atual, decimal anterior)
    {
        if (anterior == 0)
            return null;
        return Math.Round((atual - anterior) / anterior * 100, 2);
    }

    private static decimal CalcularTicketMedioGeral(List<ClienteMetricaMensal> meses)
    {
        var totalFaturamento = meses.Sum(m => m.FaturamentoTotal);
        var totalNotas = meses.Sum(m => m.QuantidadeNotas);
        if (totalNotas == 0) return 0;
        return Math.Round(totalFaturamento / totalNotas, 2);
    }

    private static decimal CalcularFrequenciaCompra(
        List<ClienteMetricaMensal> meses,
        DateTime? ultimoFaturamento,
        DateTime agora)
    {
        var mesesComCompra = meses.Count(m => m.FaturamentoTotal > 0);
        if (mesesComCompra <= 1) return 0;
        if (ultimoFaturamento is null) return 0;

        var primeiroMes = meses.LastOrDefault();
        if (primeiroMes is null) return 0;

        var periodoDias = (ultimoFaturamento.Value - primeiroMes.PrimeiroFaturamentoMes).TotalDays;
        if (periodoDias <= 0) return 0;

        return Math.Round((decimal)mesesComCompra / (decimal)(periodoDias / 30.0), 2);
    }

    private static int CalcularScoreCrescimento(decimal fat3M, decimal fatAnterior3M)
    {
        if (fatAnterior3M == 0) return fat3M > 0 ? 80 : 50;
        var variacao = (fat3M - fatAnterior3M) / fatAnterior3M;
        return variacao switch
        {
            > 0.5m => 100,
            > 0.2m => 80,
            > 0m => 60,
            > -0.2m => 40,
            _ => 20
        };
    }

    private static int CalcularScoreFrequencia(decimal frequencia)
    {
        return frequencia switch
        {
            >= 12 => 100,
            >= 6 => 80,
            >= 3 => 60,
            >= 1 => 40,
            _ => 20
        };
    }

    private static int CalcularScoreTicket(decimal ticketMedio)
    {
        return ticketMedio switch
        {
            >= 10000 => 100,
            >= 5000 => 80,
            >= 1000 => 60,
            >= 100 => 40,
            _ => 20
        };
    }

    private static int CalcularScoreRecencia(DateTime? ultimoFaturamento, DateTime agora)
    {
        if (ultimoFaturamento is null) return 0;
        var dias = (agora - ultimoFaturamento.Value).TotalDays;
        return dias switch
        {
            <= 7 => 100,
            <= 15 => 80,
            <= 30 => 60,
            <= LimiteInatividadeDias => 40,
            _ => 20
        };
    }

    private static int CalcularScorePotencial(int scoreCrescimento, int scoreFrequencia, int scoreTicket, int scoreRecencia)
    {
        var valor = (int)Math.Round(
            scoreCrescimento * 0.40m +
            scoreFrequencia * 0.30m +
            scoreTicket * 0.20m +
            scoreRecencia * 0.10m);
        return Math.Clamp(valor, 0, 100);
    }

    private static string ClassificarTendencia(List<ClienteMetricaMensal> mesesOrdenados)
    {
        if (mesesOrdenados.Count < 3) return "Estavel";

        var recentes = mesesOrdenados.Take(3).Select(m => m.FaturamentoTotal).ToList();
        var anterior = mesesOrdenados.Skip(3).Take(3).Select(m => m.FaturamentoTotal).ToList();

        if (anterior.Count == 0) return "Estavel";

        var mediaRecente = recentes.Average();
        var mediaAnterior = anterior.Average();

        if (mediaAnterior == 0) return mediaRecente > 0 ? "Crescimento" : "Estavel";

        var variacao = (mediaRecente - mediaAnterior) / mediaAnterior;
        return variacao switch
        {
            > 0.1m => "Crescimento",
            < -0.1m => "Queda",
            _ => "Estavel"
        };
    }

    private static string Classificar(int score)
    {
        return score switch
        {
            >= 80 => "A",
            >= 60 => "B",
            >= 40 => "C",
            _ => "D"
        };
    }
}
