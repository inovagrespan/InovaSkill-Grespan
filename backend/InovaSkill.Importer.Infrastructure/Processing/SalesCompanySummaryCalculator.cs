using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Processing;

public enum SalesSummaryGranularity
{
    Daily = 1,
    Weekly = 2
}

public enum SalesSummarySortBy
{
    Growth = 1,
    Amount = 2,
    Weight = 3,
    Quantity = 4
}

public sealed record SalesCompanySummaryItem(
    string CompanyName,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    decimal CurrentPeriodAmount,
    decimal PreviousPeriodAmount,
    decimal? GrowthPercent);

public sealed record SalesCompanySummaryResult(
    SalesSummaryGranularity Granularity,
    DateTime CurrentPeriodStart,
    DateTime PreviousPeriodStart,
    decimal CurrentPeriodTotalAmount,
    decimal PreviousPeriodTotalAmount,
    decimal? TotalGrowthPercent,
    int TotalRecords,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    int TotalCompanies,
    IReadOnlyList<SalesCompanySummaryItem> Items);

public static class SalesCompanySummaryCalculator
{
    private const decimal AmountMismatchTolerance = 0.05m;

    public static SalesCompanySummaryResult Build(
        IReadOnlyList<CommercialTransaction> rows,
        SalesSummaryGranularity granularity,
        SalesSummarySortBy sortBy,
        DateTime? referenceDate = null)
    {
        var current = GetPeriodKey((referenceDate ?? DateTime.UtcNow), granularity);
        var previous = granularity == SalesSummaryGranularity.Daily
            ? current.AddDays(-1)
            : current.AddDays(-7);
        var totalAmount = Round3(rows.Sum(ResolveAmount));
        var totalQuantity = Round3(rows.Sum(x => x.Quantity));
        var totalWeight = Round3(rows.Sum(x => x.GrossWeightKg));

        if (rows.Count == 0)
        {
            return new SalesCompanySummaryResult(granularity, current, previous, 0m, 0m, null, 0, totalAmount, totalQuantity, totalWeight, 0, []);
        }

        var currentPeriodTotalAmount = rows
            .Where(x => GetPeriodKey(x.TransactionDate, granularity) == current)
            .Sum(ResolveAmount);
        var previousPeriodTotalAmount = rows
            .Where(x => GetPeriodKey(x.TransactionDate, granularity) == previous)
            .Sum(ResolveAmount);
        decimal? totalGrowthPercent = null;
        if (previousPeriodTotalAmount != 0)
        {
            totalGrowthPercent = ((currentPeriodTotalAmount - previousPeriodTotalAmount) / previousPeriodTotalAmount) * 100m;
        }

        var grouped = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CustomerName) ? "Empresa não identificada" : x.CustomerName.Trim())
            .Select(g =>
            {
                var currentAmount = g
                    .Where(x => GetPeriodKey(x.TransactionDate, granularity) == current)
                    .Sum(ResolveAmount);
                var previousAmount = g
                    .Where(x => GetPeriodKey(x.TransactionDate, granularity) == previous)
                    .Sum(ResolveAmount);

                decimal? growth = null;
                if (previousAmount != 0)
                {
                    growth = ((currentAmount - previousAmount) / previousAmount) * 100m;
                }

                return new SalesCompanySummaryItem(
                    g.Key,
                    Round3(g.Sum(ResolveAmount)),
                    Round3(g.Sum(x => x.Quantity)),
                    Round3(g.Sum(x => x.GrossWeightKg)),
                    Round3(currentAmount),
                    Round3(previousAmount),
                    growth.HasValue ? Round3(growth.Value) : null);
            })
            .ToList();

        grouped = sortBy switch
        {
            SalesSummarySortBy.Amount => grouped.OrderByDescending(x => x.TotalAmount).ToList(),
            SalesSummarySortBy.Weight => grouped.OrderByDescending(x => x.TotalWeightKg).ToList(),
            SalesSummarySortBy.Quantity => grouped.OrderByDescending(x => x.TotalQuantity).ToList(),
            _ => grouped.OrderByDescending(x => x.GrowthPercent ?? decimal.MinValue).ToList()
        };

        return new SalesCompanySummaryResult(
            granularity,
            current,
            previous,
            Round3(currentPeriodTotalAmount),
            Round3(previousPeriodTotalAmount),
            totalGrowthPercent.HasValue ? Round3(totalGrowthPercent.Value) : null,
            rows.Count,
            totalAmount,
            totalQuantity,
            totalWeight,
            grouped.Count,
            grouped);
    }

    private static DateTime GetPeriodKey(DateTime value, SalesSummaryGranularity granularity)
    {
        var date = value.Date;
        if (granularity == SalesSummaryGranularity.Daily)
        {
            return date;
        }

        var diff = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(diff);
    }

    private static decimal Round3(decimal value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveAmount(CommercialTransaction row)
    {
        if (row.Quantity == 0 || row.UnitPrice == 0)
        {
            return row.TotalAmount;
        }

        var expected = row.Quantity * row.UnitPrice;
        var denominator = Math.Max(1m, Math.Abs(expected));
        var relativeDiff = Math.Abs(row.TotalAmount - expected) / denominator;
        if (relativeDiff > AmountMismatchTolerance)
        {
            return expected;
        }

        return row.TotalAmount;
    }
}
