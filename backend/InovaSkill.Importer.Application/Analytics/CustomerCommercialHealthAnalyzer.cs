namespace InovaSkill.Importer.Application.Analytics;

public sealed record CustomerCommercialHealthTransaction(
    string CustomerCode,
    string CustomerName,
    string City,
    string LinkedCompany,
    string DocumentNumber,
    string ProductCode,
    string ProductDescription,
    decimal TotalAmount,
    decimal Quantity,
    decimal GrossWeightKg,
    DateTime TransactionDate);

public sealed record CustomerCommercialHealthHeader(
    string CustomerCode,
    string CustomerName,
    string City,
    string LinkedCompany,
    DateTime? LastPurchaseDate,
    int DaysWithoutPurchase,
    decimal? AverageDaysBetweenPurchases,
    string CommercialStatus);

public sealed record CustomerCommercialHealthScore(
    int Value,
    string Label,
    string Explanation);

public sealed record CustomerCommercialHealthBlock(
    string Status,
    string Tone,
    string Summary,
    string Detail);

public sealed record CustomerCommercialHealthPotential(
    decimal? ExpectedRevenue,
    decimal? ExpectedQuantity,
    string Label,
    string Explanation);

public sealed record CustomerCommercialHealthProduct(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal Revenue,
    decimal SharePercent);

public sealed record CustomerCommercialHealthDependency(
    string Status,
    string Explanation,
    int ProductsToReachEightyPercent,
    decimal TopProductSharePercent);

public sealed record CustomerCommercialHealthTimelineItem(
    DateTime Date,
    int Orders,
    decimal Revenue,
    decimal Quantity);

public sealed record CustomerCommercialHealthEvolutionPoint(
    DateTime PeriodStart,
    decimal Revenue,
    decimal Quantity,
    int Orders,
    decimal AverageTicket);

public sealed record CustomerCommercialHealthComparison(
    string Label,
    decimal Revenue,
    decimal PreviousRevenue,
    decimal Quantity,
    decimal PreviousQuantity,
    int Orders,
    int PreviousOrders,
    decimal AverageTicket,
    decimal PreviousAverageTicket,
    decimal? RevenueVariationPercent,
    decimal? QuantityVariationPercent,
    decimal? OrdersVariationPercent,
    decimal? AverageTicketVariationPercent);

public sealed record CustomerCommercialHealthRecommendation(
    string Priority,
    string Title,
    string Detail);

public sealed record CustomerCommercialHealthAlert(
    string Severity,
    string Title,
    string Detail);

public sealed record CustomerCommercialHealthReport(
    CustomerCommercialHealthHeader Header,
    CustomerCommercialHealthScore Score,
    CustomerCommercialHealthBlock Health,
    CustomerCommercialHealthBlock Trend,
    CustomerCommercialHealthPotential Potential,
    CustomerCommercialHealthDependency Dependency,
    IReadOnlyList<CustomerCommercialHealthProduct> Products,
    IReadOnlyList<CustomerCommercialHealthTimelineItem> Timeline,
    IReadOnlyList<CustomerCommercialHealthEvolutionPoint> Evolution,
    IReadOnlyList<CustomerCommercialHealthComparison> Comparisons,
    IReadOnlyList<CustomerCommercialHealthRecommendation> Recommendations,
    IReadOnlyList<CustomerCommercialHealthAlert> Alerts);

public static class CustomerCommercialHealthAnalyzer
{
    private const int RecentMonthsForPotential = 3;
    private const int EvolutionMonths = 12;
    private const int TimelineLimit = 12;
    private const int TopProductsLimit = 8;
    private const decimal PercentMultiplier = 100m;
    private const decimal StrongTrendThresholdPercent = 25m;
    private const decimal TrendThresholdPercent = 5m;
    private const decimal ProductConcentrationAlertPercent = 50m;
    private const decimal ProductDependencyAlertPercent = 80m;
    private const decimal HighRiskCycleThreshold = 2m;

    public static CustomerCommercialHealthReport Build(
        IReadOnlyList<CustomerCommercialHealthTransaction> transactions,
        DateTime todayUtc)
    {
        var rows = transactions
            .OrderBy(x => x.TransactionDate)
            .ToList();

        if (rows.Count == 0)
        {
            return BuildEmptyReport(todayUtc);
        }

        var firstRow = rows[^1];
        var purchaseDays = rows.Select(x => x.TransactionDate.Date).Distinct().OrderBy(x => x).ToList();
        var lastPurchaseDate = purchaseDays[^1];
        var averageFrequency = AverageDaysBetweenPurchases(purchaseDays);
        var daysWithoutPurchase = Math.Max(0, (int)(todayUtc.Date - lastPurchaseDate.Date).TotalDays);
        var riskCycles = averageFrequency is > 0 ? daysWithoutPurchase / averageFrequency.Value : (decimal?)null;
        var monthly = BuildMonthlyEvolution(rows, todayUtc);
        var products = BuildProducts(rows);
        var comparisons = BuildComparisons(rows, todayUtc);
        var trend = BuildTrend(comparisons);
        var dependency = BuildDependency(products);
        var score = BuildScore(riskCycles, trend.Status, dependency.TopProductSharePercent, rows.Count);
        var health = BuildHealth(score, daysWithoutPurchase, averageFrequency, riskCycles);
        var potential = BuildPotential(monthly);
        var recommendations = BuildRecommendations(health, trend, dependency, potential, products);
        var alerts = BuildAlerts(health, trend, dependency, comparisons, daysWithoutPurchase, averageFrequency);

        return new CustomerCommercialHealthReport(
            new CustomerCommercialHealthHeader(
                firstRow.CustomerCode,
                firstRow.CustomerName,
                firstRow.City,
                firstRow.LinkedCompany,
                lastPurchaseDate,
                daysWithoutPurchase,
                averageFrequency,
                ResolveCommercialStatus(score.Value)),
            score,
            health,
            trend,
            potential,
            dependency,
            products,
            BuildTimeline(rows),
            monthly,
            comparisons,
            recommendations,
            alerts);
    }

    private static CustomerCommercialHealthReport BuildEmptyReport(DateTime todayUtc)
    {
        var health = new CustomerCommercialHealthBlock(
            "Sem histórico",
            "neutral",
            "Cliente sem compras registradas.",
            "Ainda não há dados suficientes para medir saúde comercial.");

        return new CustomerCommercialHealthReport(
            new CustomerCommercialHealthHeader(string.Empty, string.Empty, string.Empty, string.Empty, null, 0, null, "Sem histórico"),
            new CustomerCommercialHealthScore(0, "Sem histórico", "Score indisponível sem compras registradas."),
            health,
            new CustomerCommercialHealthBlock("Sem tendência", "neutral", "Sem série histórica.", "Inclua compras para calcular evolução."),
            new CustomerCommercialHealthPotential(null, null, "Sem base", "Histórico insuficiente para estimar potencial esperado."),
            new CustomerCommercialHealthDependency("Sem base", "Sem produtos para avaliar concentração.", 0, 0m),
            [],
            [],
            [],
            [],
            [new CustomerCommercialHealthRecommendation("Média", "Cadastrar histórico", "Importar compras do cliente antes de definir ação comercial.")],
            [new CustomerCommercialHealthAlert("info", "Sem histórico comercial", $"Nenhuma compra encontrada até {todayUtc:yyyy-MM-dd}.")]);
    }

    private static CustomerCommercialHealthScore BuildScore(decimal? riskCycles, string trendStatus, decimal topProductSharePercent, int transactionCount)
    {
        if (transactionCount == 0)
        {
            return new CustomerCommercialHealthScore(0, "Sem histórico", "Não há compras para calcular score.");
        }

        var score = 100m;
        if (riskCycles.HasValue)
        {
            score -= Math.Min(55m, riskCycles.Value * 18m);
        }
        else
        {
            score -= 18m;
        }

        if (trendStatus == "Forte queda") score -= 18m;
        else if (trendStatus == "Queda") score -= 10m;
        else if (trendStatus == "Forte crescimento") score += 5m;

        if (topProductSharePercent >= ProductDependencyAlertPercent) score -= 12m;
        else if (topProductSharePercent >= ProductConcentrationAlertPercent) score -= 6m;

        var value = (int)Math.Round(Math.Clamp(score, 0m, 100m), MidpointRounding.AwayFromZero);
        return new CustomerCommercialHealthScore(value, ResolveScoreLabel(value), BuildScoreExplanation(value, riskCycles, trendStatus));
    }

    private static CustomerCommercialHealthBlock BuildHealth(
        CustomerCommercialHealthScore score,
        int daysWithoutPurchase,
        decimal? averageFrequency,
        decimal? riskCycles)
    {
        var status = score.Value switch
        {
            >= 70 => "Saudável",
            >= 50 => "Atenção",
            >= 25 => "Em risco",
            _ => "Crítico"
        };

        var tone = score.Value switch
        {
            >= 70 => "success",
            >= 50 => "warning",
            _ => "danger"
        };

        var cycleText = riskCycles.HasValue
            ? $"{Math.Round(riskCycles.Value, 1)} ciclos sem compra."
            : "Frequência histórica insuficiente para medir ciclos sem compra.";

        var frequencyText = averageFrequency.HasValue
            ? $"Histórico médio é de {Math.Round(averageFrequency.Value, 1)} dias."
            : "Histórico médio ainda indisponível.";

        return new CustomerCommercialHealthBlock(
            status,
            tone,
            $"Cliente está há {daysWithoutPurchase} dias sem comprar.",
            $"{frequencyText} {cycleText}");
    }

    private static CustomerCommercialHealthBlock BuildTrend(IReadOnlyList<CustomerCommercialHealthComparison> comparisons)
    {
        var quarter = comparisons.FirstOrDefault(x => x.Label == "Últimos 90 dias");
        var variation = quarter?.RevenueVariationPercent;
        if (variation is null)
        {
            return new CustomerCommercialHealthBlock(
                "Estável",
                "neutral",
                "Sem base anterior suficiente para confirmar tendência.",
                "Acompanhe novos períodos para diferenciar estabilidade de falta de histórico.");
        }

        if (variation <= -StrongTrendThresholdPercent)
        {
            return new CustomerCommercialHealthBlock("Forte queda", "danger", $"Consumo caiu {Math.Abs(variation.Value):0.0}% nos últimos 90 dias.", "Investigar preço, estoque, concorrência ou perda de relacionamento.");
        }

        if (variation <= -TrendThresholdPercent)
        {
            return new CustomerCommercialHealthBlock("Queda", "warning", $"Consumo caiu {Math.Abs(variation.Value):0.0}% nos últimos 90 dias.", "Validar se a queda é pontual ou recorrente.");
        }

        if (variation >= StrongTrendThresholdPercent)
        {
            return new CustomerCommercialHealthBlock("Forte crescimento", "success", $"Consumo cresceu {variation.Value:0.0}% nos últimos 90 dias.", "Oportunidade para ampliar contrato, volume ou mix.");
        }

        if (variation >= TrendThresholdPercent)
        {
            return new CustomerCommercialHealthBlock("Crescimento", "success", $"Consumo cresceu {variation.Value:0.0}% nos últimos 90 dias.", "Manter cadência comercial e testar expansão controlada.");
        }

        return new CustomerCommercialHealthBlock("Estável", "neutral", $"Variação de {variation.Value:0.0}% nos últimos 90 dias.", "Consumo sem oscilação relevante no período.");
    }

    private static CustomerCommercialHealthPotential BuildPotential(IReadOnlyList<CustomerCommercialHealthEvolutionPoint> monthly)
    {
        var recent = monthly
            .Where(x => x.Revenue > 0 || x.Quantity > 0)
            .TakeLast(RecentMonthsForPotential)
            .ToList();

        if (recent.Count == 0)
        {
            return new CustomerCommercialHealthPotential(null, null, "Sem base", "Sem meses recentes com compra para estimar potencial.");
        }

        return new CustomerCommercialHealthPotential(
            recent.Average(x => x.Revenue),
            recent.Average(x => x.Quantity),
            "Potencial esperado",
            $"Baseado na média dos últimos {recent.Count} meses com movimento.");
    }

    private static IReadOnlyList<CustomerCommercialHealthProduct> BuildProducts(IReadOnlyList<CustomerCommercialHealthTransaction> rows)
    {
        var totalRevenue = rows.Sum(x => x.TotalAmount);
        return rows
            .GroupBy(x => new { x.ProductCode, x.ProductDescription })
            .Select(g =>
            {
                var revenue = g.Sum(x => x.TotalAmount);
                return new CustomerCommercialHealthProduct(
                    g.Key.ProductCode,
                    g.Key.ProductDescription,
                    g.Sum(x => x.Quantity),
                    revenue,
                    totalRevenue == 0 ? 0 : revenue / totalRevenue * PercentMultiplier);
            })
            .OrderByDescending(x => x.Revenue)
            .Take(TopProductsLimit)
            .ToList();
    }

    private static CustomerCommercialHealthDependency BuildDependency(IReadOnlyList<CustomerCommercialHealthProduct> products)
    {
        if (products.Count == 0)
        {
            return new CustomerCommercialHealthDependency("Sem base", "Sem produtos para avaliar concentração.", 0, 0m);
        }

        var cumulative = 0m;
        var productsToReachThreshold = 0;
        foreach (var product in products)
        {
            productsToReachThreshold++;
            cumulative += product.SharePercent;
            if (cumulative >= ProductDependencyAlertPercent) break;
        }

        var topShare = products[0].SharePercent;
        var status = productsToReachThreshold <= 2 ? "Alta dependência" : topShare >= ProductConcentrationAlertPercent ? "Concentração relevante" : "Mix distribuído";
        var explanation = productsToReachThreshold <= 2
            ? $"{Math.Round(ProductDependencyAlertPercent)}% do faturamento vem de apenas {productsToReachThreshold} produto(s)."
            : $"Top {Math.Min(products.Count, productsToReachThreshold)} produtos concentram {Math.Round(cumulative, 1)}% do faturamento.";

        return new CustomerCommercialHealthDependency(status, explanation, productsToReachThreshold, topShare);
    }

    private static IReadOnlyList<CustomerCommercialHealthTimelineItem> BuildTimeline(IReadOnlyList<CustomerCommercialHealthTransaction> rows)
    {
        return rows
            .GroupBy(x => x.TransactionDate.Date)
            .OrderByDescending(x => x.Key)
            .Take(TimelineLimit)
            .OrderBy(x => x.Key)
            .Select(g => new CustomerCommercialHealthTimelineItem(
                g.Key,
                g.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                g.Sum(x => x.TotalAmount),
                g.Sum(x => x.Quantity)))
            .ToList();
    }

    private static IReadOnlyList<CustomerCommercialHealthEvolutionPoint> BuildMonthlyEvolution(
        IReadOnlyList<CustomerCommercialHealthTransaction> rows,
        DateTime todayUtc)
    {
        var end = new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = end.AddMonths(-(EvolutionMonths - 1));
        var points = new List<CustomerCommercialHealthEvolutionPoint>();

        for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
        {
            var next = cursor.AddMonths(1);
            var monthRows = rows.Where(x => x.TransactionDate >= cursor && x.TransactionDate < next).ToList();
            var revenue = monthRows.Sum(x => x.TotalAmount);
            var orders = DistinctOrderCount(monthRows);
            points.Add(new CustomerCommercialHealthEvolutionPoint(
                cursor,
                revenue,
                monthRows.Sum(x => x.Quantity),
                orders,
                orders == 0 ? 0 : revenue / orders));
        }

        return points;
    }

    private static IReadOnlyList<CustomerCommercialHealthComparison> BuildComparisons(
        IReadOnlyList<CustomerCommercialHealthTransaction> rows,
        DateTime todayUtc)
    {
        var today = todayUtc.Date.AddDays(1);
        return
        [
            BuildComparison(rows, "Últimos 30 dias", today.AddDays(-30), today, today.AddDays(-60), today.AddDays(-30)),
            BuildComparison(rows, "Últimos 90 dias", today.AddDays(-90), today, today.AddDays(-180), today.AddDays(-90)),
            BuildComparison(
                rows,
                "Mês atual",
                new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                today,
                new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1),
                new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
        ];
    }

    private static CustomerCommercialHealthComparison BuildComparison(
        IReadOnlyList<CustomerCommercialHealthTransaction> rows,
        string label,
        DateTime currentFrom,
        DateTime currentTo,
        DateTime previousFrom,
        DateTime previousTo)
    {
        var current = rows.Where(x => x.TransactionDate >= currentFrom && x.TransactionDate < currentTo).ToList();
        var previous = rows.Where(x => x.TransactionDate >= previousFrom && x.TransactionDate < previousTo).ToList();
        var revenue = current.Sum(x => x.TotalAmount);
        var previousRevenue = previous.Sum(x => x.TotalAmount);
        var quantity = current.Sum(x => x.Quantity);
        var previousQuantity = previous.Sum(x => x.Quantity);
        var orders = DistinctOrderCount(current);
        var previousOrders = DistinctOrderCount(previous);
        var averageTicket = orders == 0 ? 0 : revenue / orders;
        var previousAverageTicket = previousOrders == 0 ? 0 : previousRevenue / previousOrders;

        return new CustomerCommercialHealthComparison(
            label,
            revenue,
            previousRevenue,
            quantity,
            previousQuantity,
            orders,
            previousOrders,
            averageTicket,
            previousAverageTicket,
            CustomerCalculators.CalculateVariationPercent(revenue, previousRevenue),
            CustomerCalculators.CalculateVariationPercent(quantity, previousQuantity),
            CustomerCalculators.CalculateVariationPercent(orders, previousOrders),
            CustomerCalculators.CalculateVariationPercent(averageTicket, previousAverageTicket));
    }

    private static IReadOnlyList<CustomerCommercialHealthRecommendation> BuildRecommendations(
        CustomerCommercialHealthBlock health,
        CustomerCommercialHealthBlock trend,
        CustomerCommercialHealthDependency dependency,
        CustomerCommercialHealthPotential potential,
        IReadOnlyList<CustomerCommercialHealthProduct> products)
    {
        var mainProduct = products.FirstOrDefault()?.ProductDescription;
        var productDetail = string.IsNullOrWhiteSpace(mainProduct) ? string.Empty : $" Começar por {mainProduct}.";
        var recommendations = new List<CustomerCommercialHealthRecommendation>();

        if (health.Status is "Crítico" or "Em risco")
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Alta", "Contato imediato", $"Cliente acima do ciclo esperado de recompra.{productDetail}"));
        }

        if (trend.Status is "Queda" or "Forte queda")
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Alta", "Investigar queda de consumo", "Validar preço, estoque, concorrência e substituição de produto."));
        }

        if (dependency.Status == "Alta dependência")
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Média", "Reduzir dependência de mix", dependency.Explanation));
        }

        if ((trend.Status is "Crescimento" or "Forte crescimento") && health.Status == "Saudável")
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Média", "Expandir relacionamento", "Cliente em crescimento; avaliar volume, contrato ou itens complementares."));
        }

        if (potential.ExpectedRevenue.HasValue && recommendations.Count == 0)
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Baixa", "Manter cadência consultiva", "Usar o potencial esperado como referência para a próxima abordagem."));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new CustomerCommercialHealthRecommendation("Baixa", "Acompanhar próximo ciclo", "Sem alerta forte; manter rotina de relacionamento."));
        }

        return recommendations;
    }

    private static IReadOnlyList<CustomerCommercialHealthAlert> BuildAlerts(
        CustomerCommercialHealthBlock health,
        CustomerCommercialHealthBlock trend,
        CustomerCommercialHealthDependency dependency,
        IReadOnlyList<CustomerCommercialHealthComparison> comparisons,
        int daysWithoutPurchase,
        decimal? averageFrequency)
    {
        var alerts = new List<CustomerCommercialHealthAlert>();

        if (averageFrequency.HasValue && daysWithoutPurchase > averageFrequency.Value * HighRiskCycleThreshold)
        {
            alerts.Add(new CustomerCommercialHealthAlert("critical", "Cliente sem compra acima da frequência histórica", $"{daysWithoutPurchase} dias sem compra contra média de {Math.Round(averageFrequency.Value, 1)} dias."));
        }

        if (trend.Status is "Queda" or "Forte queda")
        {
            alerts.Add(new CustomerCommercialHealthAlert("warning", "Queda relevante de faturamento", trend.Summary));
        }

        var thirtyDays = comparisons.FirstOrDefault(x => x.Label == "Últimos 30 dias");
        if (thirtyDays?.QuantityVariationPercent <= -TrendThresholdPercent)
        {
            alerts.Add(new CustomerCommercialHealthAlert("warning", "Queda de quantidade", $"Quantidade caiu {Math.Abs(thirtyDays.QuantityVariationPercent.Value):0.0}% nos últimos 30 dias."));
        }

        if (dependency.Status == "Alta dependência")
        {
            alerts.Add(new CustomerCommercialHealthAlert("warning", "Dependência de produto relevante", dependency.Explanation));
        }

        if (alerts.Count == 0 && health.Status == "Saudável")
        {
            alerts.Add(new CustomerCommercialHealthAlert("info", "Sem alerta crítico", "Cliente dentro dos principais limites comerciais monitorados."));
        }

        return alerts;
    }

    private static string ResolveScoreLabel(int score)
    {
        return score switch
        {
            >= 90 => "Excelente",
            >= 70 => "Saudável",
            >= 50 => "Atenção",
            >= 25 => "Risco",
            _ => "Crítico"
        };
    }

    private static string ResolveCommercialStatus(int score)
    {
        return score switch
        {
            >= 90 => "Excelente",
            >= 70 => "Saudável",
            >= 50 => "Atenção",
            >= 25 => "Em risco",
            _ => "Crítico"
        };
    }

    private static string BuildScoreExplanation(int score, decimal? riskCycles, string trendStatus)
    {
        var cycleText = riskCycles.HasValue
            ? $"Risco temporal em {Math.Round(riskCycles.Value, 1)} ciclos."
            : "Sem frequência média confiável.";

        return $"Score {score}/100. {cycleText} Tendência: {trendStatus}.";
    }

    private static decimal? AverageDaysBetweenPurchases(IReadOnlyList<DateTime> purchaseDays)
    {
        if (purchaseDays.Count < 2)
        {
            return null;
        }

        var deltas = purchaseDays.Zip(purchaseDays.Skip(1), (a, b) => (decimal)(b - a).TotalDays).ToList();
        return deltas.Count == 0 ? null : deltas.Average();
    }

    private static int DistinctOrderCount(IEnumerable<CustomerCommercialHealthTransaction> rows)
    {
        return rows.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }
}
