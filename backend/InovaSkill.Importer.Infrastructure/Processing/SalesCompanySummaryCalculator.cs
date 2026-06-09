using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.Processing;

public enum SalesSummaryGranularity
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
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
    int DocumentCount,
    string? SingleDocumentNumber,
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
    public static SalesCompanySummaryResult Build(
        IReadOnlyList<CommercialTransaction> rows,
        SalesSummaryGranularity granularity,
        SalesSummarySortBy sortBy,
        DateTime? referenceDate = null)
    {
        var current = GetPeriodKey((referenceDate ?? DateTime.UtcNow), granularity);
        var previous = granularity switch
        {
            SalesSummaryGranularity.Daily => current.AddDays(-1),
            SalesSummaryGranularity.Weekly => current.AddDays(-7),
            _ => current.AddMonths(-1)
        };
        var totalAmount = Round3(rows.Sum(CalculateTransactionAmount));
        var totalQuantity = Round3(rows.Sum(x => x.Quantity));
        var totalWeight = Round3(rows.Sum(x => x.GrossWeightKg));

        if (rows.Count == 0)
        {
            return new SalesCompanySummaryResult(granularity, current, previous, 0m, 0m, null, 0, totalAmount, totalQuantity, totalWeight, 0, []);
        }

        var currentPeriodTotalAmount = rows
            .Where(x => GetPeriodKey(x.TransactionDate, granularity) == current)
            .Sum(CalculateTransactionAmount);
        var previousPeriodTotalAmount = rows
            .Where(x => GetPeriodKey(x.TransactionDate, granularity) == previous)
            .Sum(CalculateTransactionAmount);
        decimal? totalGrowthPercent = null;
        if (previousPeriodTotalAmount != 0)
        {
            totalGrowthPercent = ((currentPeriodTotalAmount - previousPeriodTotalAmount) / previousPeriodTotalAmount) * 100m;
        }

        var grouped = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CustomerName) ? "Empresa não identificada" : x.CustomerName.Trim())
            .Select(g =>
            {
                var documents = g
                    .Select(x => x.DocumentNumber?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var currentAmount = g
                    .Where(x => GetPeriodKey(x.TransactionDate, granularity) == current)
                    .Sum(CalculateTransactionAmount);
                var previousAmount = g
                    .Where(x => GetPeriodKey(x.TransactionDate, granularity) == previous)
                    .Sum(CalculateTransactionAmount);

                decimal? growth = null;
                if (previousAmount != 0)
                {
                    growth = ((currentAmount - previousAmount) / previousAmount) * 100m;
                }

                return new SalesCompanySummaryItem(
                    g.Key,
                    documents.Count,
                    documents.Count == 1 ? documents[0] : null,
                    Round3(g.Sum(CalculateTransactionAmount)),
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

        if (granularity == SalesSummaryGranularity.Monthly)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        var diff = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(diff);
    }

    private static decimal Round3(decimal value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateTransactionAmount(CommercialTransaction row)
    {
        return row.Quantity * row.UnitPrice;
    }
}
