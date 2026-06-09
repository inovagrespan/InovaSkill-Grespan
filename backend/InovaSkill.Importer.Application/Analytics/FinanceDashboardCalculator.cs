using System.Globalization;

namespace InovaSkill.Importer.Application.Analytics;

public sealed record FinanceDashboardTransaction(
    string Customer,
    DateTime Date,
    decimal Revenue,
    string DocumentNumber,
    decimal Quantity);

public sealed record FinanceDashboardItem(
    string Customer,
    DateTime Date,
    decimal Revenue,
    int Orders,
    decimal Quantity);

public sealed record FinanceDashboardSummary(
    decimal TotalRevenue,
    int TotalOrders,
    decimal TotalQuantity,
    decimal AverageTicket);

public sealed record FinanceRevenueTrendPoint(
    string Period,
    string Label,
    decimal Revenue);

public sealed record FinanceCustomerRevenuePoint(
    string Customer,
    decimal Revenue);

public static class FinanceDashboardCalculator
{
    private const int DaysInWeek = 7;
    private const int TopCustomersLimit = 5;
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    public static IReadOnlyList<string> ListCustomers(IReadOnlyList<FinanceDashboardTransaction> transactions)
    {
        return transactions
            .Select(x => x.Customer.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Create(PtBrCulture, ignoreCase: true))
            .ToList();
    }

    public static IReadOnlyList<FinanceDashboardItem> BuildItems(
        IReadOnlyList<FinanceDashboardTransaction> transactions,
        string customer,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool allTime)
    {
        var filtered = FilterTransactions(transactions, customer, dateFrom, dateTo, allTime);

        return filtered
            .GroupBy(x => new { Customer = x.Customer.Trim(), Date = NormalizeToUtc(x.Date).Date })
            .Select(group => new FinanceDashboardItem(
                group.Key.Customer,
                group.Key.Date,
                group.Sum(x => x.Revenue),
                group.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Sum(x => x.Quantity)))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Customer, StringComparer.Create(PtBrCulture, ignoreCase: true))
            .ToList();
    }

    public static FinanceDashboardSummary BuildSummary(IReadOnlyList<FinanceDashboardItem> items)
    {
        var totalRevenue = items.Sum(x => x.Revenue);
        var totalOrders = items.Sum(x => x.Orders);
        var totalQuantity = items.Sum(x => x.Quantity);

        return new FinanceDashboardSummary(
            totalRevenue,
            totalOrders,
            totalQuantity,
            totalOrders == 0 ? 0 : totalRevenue / totalOrders);
    }

    public static IReadOnlyList<FinanceRevenueTrendPoint> BuildRevenueTrend(
        IReadOnlyList<FinanceDashboardItem> items,
        string granularity)
    {
        return items
            .GroupBy(x => ResolvePeriod(x, granularity))
            .Select(group => new FinanceRevenueTrendPoint(
                group.Key.Period,
                group.Key.Label,
                group.Sum(x => x.Revenue)))
            .OrderBy(x => x.Period, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<FinanceCustomerRevenuePoint> BuildCustomerRevenueRanking(IReadOnlyList<FinanceDashboardItem> items)
    {
        return items
            .GroupBy(x => x.Customer.Trim())
            .Select(group => new FinanceCustomerRevenuePoint(group.Key, group.Sum(x => x.Revenue)))
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Customer, StringComparer.Create(PtBrCulture, ignoreCase: true))
            .Take(TopCustomersLimit)
            .ToList();
    }

    private static IEnumerable<FinanceDashboardTransaction> FilterTransactions(
        IReadOnlyList<FinanceDashboardTransaction> transactions,
        string customer,
        DateTime? dateFrom,
        DateTime? dateTo,
        bool allTime)
    {
        var normalizedCustomer = customer.Trim();
        var hasCustomerFilter = !string.IsNullOrWhiteSpace(normalizedCustomer);
        var fromInclusive = dateFrom.HasValue ? NormalizeToUtc(dateFrom.Value).Date : (DateTime?)null;
        var toExclusive = dateTo.HasValue ? NormalizeToUtc(dateTo.Value).Date.AddDays(1) : (DateTime?)null;

        return transactions.Where(item =>
        {
            if (hasCustomerFilter &&
                item.Customer.Contains(normalizedCustomer, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            if (allTime)
            {
                return true;
            }

            var itemDate = NormalizeToUtc(item.Date);
            if (fromInclusive.HasValue && itemDate < fromInclusive.Value) return false;
            if (toExclusive.HasValue && itemDate >= toExclusive.Value) return false;
            return true;
        });
    }

    private static (string Period, string Label) ResolvePeriod(FinanceDashboardItem item, string granularity)
    {
        var normalizedDate = NormalizeToUtc(item.Date).Date;
        var normalizedGranularity = granularity.Trim().ToLowerInvariant();
        if (normalizedGranularity == "weekly")
        {
            var weekStart = StartOfWeek(normalizedDate);
            return (weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"Sem {weekStart:dd/MM}");
        }

        if (normalizedGranularity == "yearly")
        {
            var year = normalizedDate.Year.ToString(CultureInfo.InvariantCulture);
            return (year, year);
        }

        var period = normalizedDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var label = normalizedDate.ToString("MMM", PtBrCulture).Replace(".", string.Empty).ToLowerInvariant();
        return (period, label);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var mondayOffset = ((int)date.DayOfWeek + 6) % DaysInWeek;
        return date.AddDays(-mondayOffset);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
