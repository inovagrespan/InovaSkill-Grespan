using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/analytics-financeiro")]
public sealed class AnalyticsFinanceiroController(ImportDbContext dbContext) : ControllerBase
{
    private const int MonthsInQuarter = 3;
    private const int MonthsInSemester = 6;
    private const int MonthsInYear = 12;
    private const int TopCustomersLimit = 10;
    private const int RecentPurchaseDays = 7;
    private const int WarmPurchaseDays = 15;
    private const int ActivePurchaseDays = 30;
    private const int InactivePurchaseDays = 90;
    private const decimal StrongGrowthPercentThreshold = 10m;
    private const decimal HighTicketThreshold = 10_000m;
    private const decimal MediumHighTicketThreshold = 5_000m;
    private const decimal MediumTicketThreshold = 1_000m;
    private const decimal LowTicketThreshold = 100m;
    private const decimal HighFrequencyThreshold = 12m;
    private const decimal MediumHighFrequencyThreshold = 6m;
    private const decimal MediumFrequencyThreshold = 3m;
    private const decimal LowFrequencyThreshold = 1m;
    private const int ProjectionCustomerLimit = 20;
    private const int ProjectionMonthsInQuarter = 3;
    private const int ProjectionMonthsInSemester = 6;
    private const int ProjectionMonthsInYear = 12;
    private const decimal ProjectionGrowthSensitivity = 0.50m;
    private const decimal ProjectionMinimumGrowthFactor = 0.70m;
    private const decimal ProjectionMaximumGrowthFactor = 1.50m;
    private const decimal HighForecastConfidence = 0.85m;
    private const decimal MediumForecastConfidence = 0.70m;
    private const decimal LowForecastConfidence = 0.55m;

    [HttpGet("impacto")]
    public async Task<ActionResult> Impacto(CancellationToken ct)
    {
        var indicadores = await dbContext.ClienteIndicadores.AsNoTracking().ToListAsync(ct);
        if (indicadores.Count == 0)
        {
            indicadores = await BuildIndicadoresFromCommercialTransactionsAsync(ct);
        }

        var nomes = await dbContext.CustomerSummariesDaily
            .AsNoTracking()
            .Where(x => x.CustomerName != null && x.CustomerName.Trim() != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        var nomesClientes = nomes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerName.Trim(), StringComparer.OrdinalIgnoreCase);
        var nomesTransacoes = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode != "" && x.CustomerName != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        foreach (var nome in nomesTransacoes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { CustomerCode = g.Key, CustomerName = g.First().CustomerName.Trim() }))
        {
            nomesClientes.TryAdd(nome.CustomerCode, nome.CustomerName);
        }

        var fornecedoresTransacoes = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode != "" && (x.SupplierName != "" || x.SupplierCode != "" || x.RouteName != "" || x.City != ""))
            .Select(x => new { x.CustomerCode, x.SupplierCode, x.SupplierName, x.RouteName, x.City, x.TransactionDate })
            .ToListAsync(ct);
        var fornecedoresClientes = fornecedoresTransacoes
            .OrderByDescending(x => x.TransactionDate)
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var item = g.First();
                    return new
                    {
                        FornecedorId = item.SupplierCode.Trim(),
                        FornecedorNome = item.SupplierName.Trim(),
                        RotaNome = string.IsNullOrWhiteSpace(item.RouteName) ? item.City.Trim() : item.RouteName.Trim()
                    };
                },
                StringComparer.OrdinalIgnoreCase);

        if (indicadores.Count == 0)
            return Ok(new { risco = Array.Empty<object>(), crescimento = Array.Empty<object>(), resumo = new { }, alertas = Array.Empty<object>() });

        var totalFaturamento = indicadores.Sum(i => i.Faturamento12M);

        var clientesRisco = indicadores
            .Where(i => i.Tendencia == "Queda" || i.Classificacao == "D" || i.ScoreRecencia < 40)
            .OrderBy(i => i.Crescimento12M ?? 0)
            .Take(TopCustomersLimit)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                fornecedorId = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorId ?? "",
                fornecedorNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorNome ?? "",
                rotaNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.RotaNome ?? "",
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
            .Take(TopCustomersLimit)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                fornecedorId = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorId ?? "",
                fornecedorNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorNome ?? "",
                rotaNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.RotaNome ?? "",
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
            .Take(TopCustomersLimit)
            .Select(i => new
            {
                i.ClienteId,
                clienteNome = nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                fornecedorId = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorId ?? "",
                fornecedorNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.FornecedorNome ?? "",
                rotaNome = fornecedoresClientes.GetValueOrDefault(i.ClienteId)?.RotaNome ?? "",
                i.ScorePotencial,
                i.Crescimento12M,
                i.Faturamento12M,
                potencial = ClassificarPotencial(i.ScorePotencial),
                i.TicketMedioGeral,
                i.FrequenciaCompra
            })
            .ToList();

        var maior = indicadores.OrderByDescending(i => i.Faturamento12M).FirstOrDefault();
        var maiorCrescimento = indicadores.Where(i => ObterVariacaoResumo(i) > 0).OrderByDescending(ObterVariacaoResumo).FirstOrDefault();
        var maiorQueda = indicadores.Where(i => ObterVariacaoResumo(i) < 0).OrderBy(ObterVariacaoResumo).FirstOrDefault();
        var maisConsistente = indicadores.OrderBy(i => Math.Abs((double)(ObterVariacaoResumo(i) ?? 0))).FirstOrDefault();
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
                maiorCrescimentoPct = ObterVariacaoResumo(maiorCrescimento),
                maiorQuedaNome = maiorQueda?.ClienteId is not null ? nomesClientes.GetValueOrDefault(maiorQueda.ClienteId) ?? maiorQueda.ClienteId : null,
                maiorQuedaPct = ObterVariacaoResumo(maiorQueda),
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
        if (indicadores.Count == 0)
        {
            indicadores = await BuildIndicadoresFromCommercialTransactionsAsync(ct);
        }

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
        var nomesTransacoes = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.CustomerCode != "" && x.CustomerName != "")
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .Distinct()
            .ToListAsync(ct);
        foreach (var nome in nomesTransacoes
            .GroupBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { CustomerCode = g.Key, CustomerName = g.First().CustomerName.Trim() }))
        {
            nomesClientes.TryAdd(nome.CustomerCode, nome.CustomerName);
        }

        var evolucaoClientes = clientesComForecast.Any()
            ? forecasts.Take(ProjectionCustomerLimit).Select(f =>
            {
                var ind = indicadores.FirstOrDefault(i => i.ClienteId == f.ClienteId);
                var mediaMensal = ind?.Faturamento3M > 0 ? ind.Faturamento3M / ProjectionMonthsInQuarter : 0;
                return new ProjectionRow(
                    f.ClienteId,
                    nomesClientes.GetValueOrDefault(f.ClienteId) ?? f.ClienteId,
                    Math.Round(mediaMensal, 2),
                    Math.Round(f.Previsao30Dias, 2),
                    Math.Round(f.Previsao30Dias - mediaMensal, 2),
                    f.TendenciaPrevista,
                    f.ConfiancaModelo);
            }).ToList()
            : BuildProjectionRowsFromIndicadores(indicadores, nomesClientes);

        var proj30 = clientesComForecast.Any()
            ? forecasts.Sum(f => f.Previsao30Dias)
            : evolucaoClientes.Sum(x => x.valorProjetado);
        var proj90 = clientesComForecast.Any()
            ? forecasts.Sum(f => f.Previsao30Dias + f.Previsao60Dias + f.Previsao90Dias)
            : proj30 * ProjectionMonthsInQuarter;
        var proj180 = clientesComForecast.Any()
            ? proj90 + forecasts.Sum(f => f.Previsao90Dias)
            : proj30 * ProjectionMonthsInSemester;
        var proj360 = clientesComForecast.Any()
            ? proj180 * 2
            : proj30 * ProjectionMonthsInYear;

        var faturamentoAtual = indicadores.Sum(i => i.Faturamento12M) / ProjectionMonthsInYear;

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
            evolucaoClientes
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

    private static decimal? ObterVariacaoResumo(Domain.Entities.ClienteIndicador? indicador)
    {
        return indicador?.Crescimento12M ?? indicador?.Crescimento6M ?? indicador?.Crescimento3M;
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

    private async Task<List<Domain.Entities.ClienteIndicador>> BuildIndicadoresFromCommercialTransactionsAsync(CancellationToken ct)
    {
        var transactions = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.TotalAmount != 0 && (x.CustomerCode != "" || x.CustomerName != ""))
            .Select(x => new FinanceImpactTransaction(
                x.CustomerCode,
                x.CustomerName,
                x.DocumentNumber,
                x.TransactionDate,
                x.TotalAmount))
            .ToListAsync(ct);

        if (transactions.Count == 0)
        {
            return [];
        }

        var referenceDate = transactions.Max(x => x.TransactionDate).Date;
        var current3Start = StartOfMonth(referenceDate.AddMonths(-(MonthsInQuarter - 1)));
        var current6Start = StartOfMonth(referenceDate.AddMonths(-(MonthsInSemester - 1)));
        var current12Start = StartOfMonth(referenceDate.AddMonths(-(MonthsInYear - 1)));
        var previous3Start = StartOfMonth(current3Start.AddMonths(-MonthsInQuarter));
        var previous6Start = StartOfMonth(current6Start.AddMonths(-MonthsInSemester));
        var previous12Start = StartOfMonth(current12Start.AddMonths(-MonthsInYear));

        return transactions
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CustomerCode) ? x.CustomerName.Trim() : x.CustomerCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(x => x.TransactionDate).ToList();
                var faturamento3M = SumBetween(ordered, current3Start, referenceDate);
                var faturamento6M = SumBetween(ordered, current6Start, referenceDate);
                var faturamento12M = SumBetween(ordered, current12Start, referenceDate);
                var faturamentoAnterior3M = SumBetween(ordered, previous3Start, current3Start.AddDays(-1));
                var faturamentoAnterior6M = SumBetween(ordered, previous6Start, current6Start.AddDays(-1));
                var faturamentoAnterior12M = SumBetween(ordered, previous12Start, current12Start.AddDays(-1));
                var documents12M = ordered
                    .Where(x => IsBetween(x.TransactionDate, current12Start, referenceDate))
                    .Select(x => x.DocumentNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var ticketMedio = documents12M > 0 ? Math.Round(faturamento12M / documents12M, 2) : 0;
                var mesesComCompra = ordered
                    .Where(x => IsBetween(x.TransactionDate, current12Start, referenceDate))
                    .Select(x => new YearMonth(x.TransactionDate.Year, x.TransactionDate.Month))
                    .Distinct()
                    .Count();
                var frequenciaCompra = Math.Round((decimal)documents12M / MonthsInYear, 2);
                var crescimento3M = CalcularVariacao(faturamento3M, faturamentoAnterior3M);
                var crescimento6M = CalcularVariacao(faturamento6M, faturamentoAnterior6M);
                var crescimento12M = CalcularVariacao(faturamento12M, faturamentoAnterior12M);
                var scoreCrescimento = CalcularScoreCrescimento(faturamento3M, faturamentoAnterior3M);
                var scoreFrequencia = CalcularScoreFrequencia(frequenciaCompra);
                var scoreTicket = CalcularScoreTicket(ticketMedio);
                var scoreRecencia = CalcularScoreRecencia(ordered.First().TransactionDate.Date, referenceDate);
                var scorePotencial = CalcularScorePotencial(scoreCrescimento, scoreFrequencia, scoreTicket, scoreRecencia);

                return new Domain.Entities.ClienteIndicador
                {
                    ClienteId = group.Key,
                    Faturamento3M = faturamento3M,
                    Faturamento6M = faturamento6M,
                    Faturamento12M = faturamento12M,
                    Crescimento3M = crescimento3M,
                    Crescimento6M = crescimento6M,
                    Crescimento12M = crescimento12M,
                    MediaMovel3M = Math.Round(faturamento3M / MonthsInQuarter, 2),
                    MediaMovel6M = Math.Round(faturamento6M / MonthsInSemester, 2),
                    MediaMovel12M = Math.Round(faturamento12M / MonthsInYear, 2),
                    FrequenciaCompra = frequenciaCompra,
                    TicketMedioGeral = ticketMedio,
                    ScoreCrescimento = scoreCrescimento,
                    ScoreFrequencia = scoreFrequencia,
                    ScoreTicket = scoreTicket,
                    ScoreRecencia = scoreRecencia,
                    ScorePotencial = scorePotencial,
                    Tendencia = ClassificarTendencia(crescimento3M, mesesComCompra),
                    Classificacao = ClassificarScore(scorePotencial),
                    AtualizadoEm = DateTime.UtcNow
                };
            })
            .Where(x => x.Faturamento12M != 0)
            .ToList();
    }

    private static DateTime StartOfMonth(DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static bool IsBetween(DateTime value, DateTime start, DateTime end)
    {
        var date = value.Date;
        return date >= start && date <= end;
    }

    private static decimal SumBetween(IEnumerable<FinanceImpactTransaction> rows, DateTime start, DateTime end)
    {
        return rows.Where(x => IsBetween(x.TransactionDate, start, end)).Sum(x => x.TotalAmount);
    }

    private static decimal? CalcularVariacao(decimal atual, decimal anterior)
    {
        if (anterior == 0)
        {
            return null;
        }

        return Math.Round((atual - anterior) / anterior * 100m, 2);
    }

    private static int CalcularScoreCrescimento(decimal atual, decimal anterior)
    {
        if (anterior == 0) return atual > 0 ? 80 : 50;
        var variacao = (atual - anterior) / anterior;
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
            >= HighFrequencyThreshold => 100,
            >= MediumHighFrequencyThreshold => 80,
            >= MediumFrequencyThreshold => 60,
            >= LowFrequencyThreshold => 40,
            _ => 20
        };
    }

    private static int CalcularScoreTicket(decimal ticketMedio)
    {
        return ticketMedio switch
        {
            >= HighTicketThreshold => 100,
            >= MediumHighTicketThreshold => 80,
            >= MediumTicketThreshold => 60,
            >= LowTicketThreshold => 40,
            _ => 20
        };
    }

    private static int CalcularScoreRecencia(DateTime ultimoFaturamento, DateTime referencia)
    {
        var dias = (referencia - ultimoFaturamento).TotalDays;
        return dias switch
        {
            <= RecentPurchaseDays => 100,
            <= WarmPurchaseDays => 80,
            <= ActivePurchaseDays => 60,
            <= InactivePurchaseDays => 40,
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

    private static string ClassificarTendencia(decimal? crescimento3M, int mesesComCompra)
    {
        if (mesesComCompra < MonthsInQuarter || crescimento3M is null) return "Estavel";
        if (crescimento3M > StrongGrowthPercentThreshold) return "Crescimento";
        if (crescimento3M < -StrongGrowthPercentThreshold) return "Queda";
        return "Estavel";
    }

    private static string ClassificarScore(int score)
    {
        return score switch
        {
            >= 80 => "A",
            >= 60 => "B",
            >= 40 => "C",
            _ => "D"
        };
    }

    private static List<ProjectionRow> BuildProjectionRowsFromIndicadores(
        IReadOnlyCollection<Domain.Entities.ClienteIndicador> indicadores,
        IReadOnlyDictionary<string, string> nomesClientes)
    {
        return indicadores
            .OrderByDescending(i => i.Faturamento12M)
            .Take(ProjectionCustomerLimit)
            .Select(i =>
            {
                var valorAtual = i.Faturamento3M > 0
                    ? i.Faturamento3M / ProjectionMonthsInQuarter
                    : i.Faturamento12M / ProjectionMonthsInYear;
                var variacao = ObterVariacaoResumo(i);
                var growthFactor = ResolveProjectionGrowthFactor(variacao);
                var valorProjetado = Math.Round(valorAtual * growthFactor, 2);
                return new ProjectionRow(
                    i.ClienteId,
                    nomesClientes.GetValueOrDefault(i.ClienteId) ?? i.ClienteId,
                    Math.Round(valorAtual, 2),
                    valorProjetado,
                    Math.Round(valorProjetado - valorAtual, 2),
                    ResolveProjectionTrend(variacao),
                    ResolveProjectionConfidence(i));
            })
            .ToList();
    }

    private static decimal ResolveProjectionGrowthFactor(decimal? variacaoPercentual)
    {
        if (variacaoPercentual is null) return 1m;

        var factor = 1m + (variacaoPercentual.Value / 100m * ProjectionGrowthSensitivity);
        return Math.Clamp(factor, ProjectionMinimumGrowthFactor, ProjectionMaximumGrowthFactor);
    }

    private static string ResolveProjectionTrend(decimal? variacaoPercentual)
    {
        if (variacaoPercentual > StrongGrowthPercentThreshold) return "Crescimento";
        if (variacaoPercentual < -StrongGrowthPercentThreshold) return "Queda";
        return "Estavel";
    }

    private static decimal ResolveProjectionConfidence(Domain.Entities.ClienteIndicador indicador)
    {
        if (indicador.FrequenciaCompra >= MediumFrequencyThreshold && indicador.ScoreRecencia >= 60)
        {
            return HighForecastConfidence;
        }

        if (indicador.FrequenciaCompra >= LowFrequencyThreshold && indicador.ScoreRecencia >= 40)
        {
            return MediumForecastConfidence;
        }

        return LowForecastConfidence;
    }

    private sealed record FinanceImpactTransaction(
        string CustomerCode,
        string CustomerName,
        string DocumentNumber,
        DateTime TransactionDate,
        decimal TotalAmount);

    private sealed record YearMonth(int Year, int Month);

    private sealed record ProjectionRow(
        string ClienteId,
        string clienteNome,
        decimal valorAtual,
        decimal valorProjetado,
        decimal diferenca,
        string TendenciaPrevista,
        decimal ConfiancaModelo);
}
