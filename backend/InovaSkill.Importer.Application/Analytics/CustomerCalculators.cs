namespace InovaSkill.Importer.Application.Analytics;

public sealed record CustomerMetricTransaction(
    string CustomerCode,
    string CustomerName,
    string City,
    string DocumentNumber,
    decimal TotalAmount,
    decimal Quantity,
    decimal GrossWeightKg,
    DateTime TransactionDate);

public sealed record CustomerPeriod(
    DateTime CurrentFrom,
    DateTime CurrentTo,
    DateTime PreviousFrom,
    DateTime PreviousTo);

public sealed record CustomerSummaryMetrics(
    DateTime? LastPurchaseDate,
    string Status,
    decimal TotalRevenue,
    decimal? AverageTicket,
    decimal? AverageRevenueMonthly,
    decimal? AverageRevenueWeekly,
    decimal TotalQuantity,
    decimal TotalWeight,
    int TotalOrders,
    decimal? AverageDaysBetweenPurchases);

public sealed record CustomerAnalyticsSummaryMetrics(
    int ActiveCustomers,
    decimal TotalRevenue,
    int TotalOrders,
    decimal AverageTicket,
    decimal AverageRevenuePerCustomer,
    int NewCustomers,
    int InactiveCustomers);

public sealed record CustomerRankingMetrics(
    string CustomerCode,
    string CustomerName,
    decimal Revenue,
    decimal Quantity,
    decimal Weight,
    int Orders,
    decimal AverageTicket,
    decimal? VariationPercent);

public sealed record CustomerMonthlyNewCustomersMetrics(
    DateTime MonthStart,
    int NewCustomers);

public sealed record CustomerProductShareMetrics(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal Revenue,
    decimal SharePercent);

public sealed record CustomerMonthlyMetric(DateTime MonthStart, decimal Revenue, decimal Quantity);

public sealed record CustomerInsightsMetrics(
    decimal? AveragePurchaseFrequencyDays,
    DateTime? EstimatedNextPurchaseDate,
    decimal? PredictedRevenue,
    decimal? PredictedQuantity,
    string ConsumptionTrend,
    string RiskLevel,
    int DaysWithoutPurchase,
    decimal? RiskScore,
    string? FrequencyReason,
    string? NextPurchaseReason,
    string? RevenuePredictionReason,
    string? QuantityPredictionReason,
    string? RiskReason,
    int MonthlyHistoryPeriods);

public static class CustomerCalculators
{
    private const decimal PercentMultiplier = 100m;
    private const int FirstDayOfMonth = 1;
    private const int DaysPerWeek = 7;
    private const int DaysFromSundayToMonday = 6;
    private const int InactiveCustomerDaysThreshold = 45;
    private const decimal WeeklyGrowthStatusThresholdPercent = 10m;
    private const decimal WeeklyDropStatusThresholdPercent = -10m;

    public static CustomerPeriod ResolvePeriods(DateTime? dateFrom, DateTime? dateTo, int defaultDays)
    {
        var normalizedTo = NormalizeToUtc(dateTo ?? DateTime.UtcNow);
        var to = normalizedTo.Date.AddDays(1);
        var from = NormalizeToUtc(dateFrom ?? to.AddDays(-defaultDays)).Date;
        if (from >= to)
        {
            from = to.AddDays(-defaultDays);
        }

        var length = to - from;
        return new CustomerPeriod(from, to, from - length, from);
    }

    public static CustomerSummaryMetrics BuildCustomerSummary(
        IReadOnlyList<CustomerMetricTransaction> allCustomerRows,
        DateTime fromInclusive,
        DateTime toExclusive,
        decimal currentWeekRevenue,
        decimal previousWeekRevenue,
        DateTime todayUtc)
    {
        var rows = allCustomerRows
            .Where(x => x.TransactionDate >= fromInclusive && x.TransactionDate < toExclusive)
            .ToList();

        var purchaseDays = rows.Select(x => x.TransactionDate.Date).Distinct().OrderBy(x => x).ToList();
        var averageDaysBetweenPurchases = AverageDaysBetweenPurchases(purchaseDays);
        var totalRevenue = rows.Sum(x => x.TotalAmount);
        var totalOrders = DistinctOrderCount(rows);
        DateTime? lastPurchaseDate = allCustomerRows.Count == 0 ? null : allCustomerRows.Max(x => x.TransactionDate);
        var status = ResolveStatus(lastPurchaseDate, currentWeekRevenue, previousWeekRevenue, todayUtc);

        var monthlyBuckets = rows
            .GroupBy(x => new DateTime(x.TransactionDate.Year, x.TransactionDate.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .Select(g => g.Sum(x => x.TotalAmount))
            .ToList();

        var weeklyBuckets = rows
            .GroupBy(x => StartOfWeekUtc(x.TransactionDate))
            .Select(g => g.Sum(x => x.TotalAmount))
            .ToList();

        return new CustomerSummaryMetrics(
            lastPurchaseDate,
            status,
            totalRevenue,
            totalOrders == 0 ? null : totalRevenue / totalOrders,
            monthlyBuckets.Count == 0 ? null : monthlyBuckets.Average(),
            weeklyBuckets.Count == 0 ? null : weeklyBuckets.Average(),
            rows.Sum(x => x.Quantity),
            rows.Sum(x => x.GrossWeightKg),
            totalOrders,
            averageDaysBetweenPurchases);
    }

    public static CustomerAnalyticsSummaryMetrics BuildAnalyticsSummary(
        IReadOnlyList<CustomerMetricTransaction> currentRows,
        IReadOnlyCollection<string> previousCustomerKeys)
    {
        var activeCustomers = currentRows
            .Select(BuildCustomerKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previousCustomers = previousCustomerKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var totalRevenue = currentRows.Sum(x => x.TotalAmount);
        var totalOrders = DistinctOrderCount(currentRows);
        var activeCount = activeCustomers.Count;

        return new CustomerAnalyticsSummaryMetrics(
            activeCount,
            totalRevenue,
            totalOrders,
            totalOrders == 0 ? 0 : totalRevenue / totalOrders,
            activeCount == 0 ? 0 : totalRevenue / activeCount,
            activeCustomers.Count(x => !previousCustomers.Contains(x)),
            previousCustomers.Count(x => !activeCustomers.Contains(x)));
    }

    public static IReadOnlyList<CustomerRankingMetrics> BuildRanking(
        IReadOnlyList<CustomerMetricTransaction> currentRows,
        IReadOnlyDictionary<string, decimal> previousRevenueByCustomerKey,
        string sortBy)
    {
        var items = currentRows
            .GroupBy(x => new { x.CustomerCode, x.CustomerName })
            .Select(g =>
            {
                var revenue = g.Sum(x => x.TotalAmount);
                var orders = DistinctOrderCount(g);
                previousRevenueByCustomerKey.TryGetValue($"{g.Key.CustomerCode}|{g.Key.CustomerName}", out var previousRevenue);
                return new CustomerRankingMetrics(
                    g.Key.CustomerCode,
                    g.Key.CustomerName,
                    revenue,
                    g.Sum(x => x.Quantity),
                    g.Sum(x => x.GrossWeightKg),
                    orders,
                    orders == 0 ? 0 : revenue / orders,
                previousRevenue == 0 ? null : ((revenue - previousRevenue) / previousRevenue) * PercentMultiplier);
            })
            .ToList();

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "growth" => items.OrderByDescending(x => x.VariationPercent ?? decimal.MinValue).ToList(),
            "drop" => items.OrderBy(x => x.VariationPercent ?? decimal.MaxValue).ToList(),
            "quantity" => items.OrderByDescending(x => x.Quantity).ToList(),
            "weight" => items.OrderByDescending(x => x.Weight).ToList(),
            "ticket" => items.OrderByDescending(x => x.AverageTicket).ToList(),
            _ => items.OrderByDescending(x => x.Revenue).ToList()
        };
    }

    public static IReadOnlyList<CustomerMonthlyNewCustomersMetrics> BuildNewCustomersMonthly(
        IReadOnlyList<DateTime> firstPurchaseDates,
        DateTime currentFrom,
        DateTime currentTo)
    {
        var monthStart = StartOfMonthUtc(currentFrom);
        var monthEndExclusive = StartOfMonthUtc(currentTo);
        if (monthEndExclusive < currentTo)
        {
            monthEndExclusive = monthEndExclusive.AddMonths(1);
        }

        var points = new List<CustomerMonthlyNewCustomersMetrics>();
        for (var cursor = monthStart; cursor < monthEndExclusive; cursor = cursor.AddMonths(1))
        {
            var nextMonth = cursor.AddMonths(1);
            var monthCount = firstPurchaseDates.Count(x => x >= cursor && x < nextMonth);
            points.Add(new CustomerMonthlyNewCustomersMetrics(cursor, monthCount));
        }

        return points;
    }

    public static IReadOnlyList<CustomerProductShareMetrics> BuildProductShares(
        IReadOnlyList<CustomerProductShareMetrics> groupedProducts)
    {
        var totalRevenue = groupedProducts.Sum(x => x.Revenue);
        return groupedProducts
            .Select(x => x with { SharePercent = totalRevenue == 0 ? 0 : (x.Revenue / totalRevenue) * PercentMultiplier })
            .ToList();
    }

    public static decimal? CalculateVariationPercent(decimal current, decimal previous)
    {
        return previous == 0 ? null : ((current - previous) / previous) * PercentMultiplier;
    }

    public static CustomerInsightsMetrics BuildInsights(
        IReadOnlyList<DateTime> purchaseDays,
        IReadOnlyList<CustomerMonthlyMetric> monthlyRows,
        int movingAverageWindowMonths,
        DateTime todayUtc,
        int minimumPeriodsForMovingAverage = 3)
    {
        return CustomerInsightsCalculator.Build(
            purchaseDays,
            monthlyRows,
            movingAverageWindowMonths,
            todayUtc,
            minimumPeriodsForMovingAverage);
    }

    public static decimal? ResolveMovingAverage(IReadOnlyList<decimal> values, int window, int minimumPeriods)
    {
        return MovingAverageCalculator.Calculate(values, window, minimumPeriods);
    }

    public static string ResolveConsumptionTrend(IReadOnlyList<decimal> quantities)
    {
        return ConsumptionTrendClassifier.Classify(quantities);
    }

    public static string ResolveRiskLevel(decimal? riskScore)
    {
        return RiskClassifier.Classify(riskScore);
    }

    public static DateTime StartOfWeekUtc(DateTime date)
    {
        var normalized = NormalizeToUtc(date).Date;
        var diff = ((int)normalized.DayOfWeek + DaysFromSundayToMonday) % DaysPerWeek;
        return normalized.AddDays(-diff);
    }

    public static DateTime StartOfMonthUtc(DateTime date)
    {
        var normalized = NormalizeToUtc(date);
        return new DateTime(normalized.Year, normalized.Month, FirstDayOfMonth, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static decimal? AverageDaysBetweenPurchases(IReadOnlyList<DateTime> purchaseDays)
    {
        if (purchaseDays.Count <= 1)
        {
            return null;
        }

        var deltas = purchaseDays.Zip(purchaseDays.Skip(1), (a, b) => (decimal)(b - a).TotalDays).ToList();
        return deltas.Count == 0 ? null : deltas.Average();
    }

    private static int DistinctOrderCount(IEnumerable<CustomerMetricTransaction> rows)
    {
        return rows.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private static string BuildCustomerKey(CustomerMetricTransaction row)
    {
        return $"{row.CustomerCode}|{row.CustomerName}";
    }

    private static string ResolveStatus(DateTime? lastPurchaseDate, decimal currentWeekRevenue, decimal previousWeekRevenue, DateTime todayUtc)
    {
        if (lastPurchaseDate is null)
        {
            return "Inativo";
        }

        var daysWithoutPurchase = (todayUtc.Date - lastPurchaseDate.Value.Date).TotalDays;
        if (daysWithoutPurchase > InactiveCustomerDaysThreshold)
        {
            return "Inativo";
        }

        if (previousWeekRevenue <= 0)
        {
            return "Ativo";
        }

        var variation = ((currentWeekRevenue - previousWeekRevenue) / previousWeekRevenue) * PercentMultiplier;
        if (variation >= WeeklyGrowthStatusThresholdPercent) return "Em crescimento";
        if (variation <= WeeklyDropStatusThresholdPercent) return "Em queda";
        return "Ativo";
    }
}
