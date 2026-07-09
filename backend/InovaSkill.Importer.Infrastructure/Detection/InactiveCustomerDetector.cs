using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class InactiveCustomerDetector(
    ImportDbContext dbContext) : IDetector
{
    public string Code => DetectorCodes.InactiveCustomer;

    private const int InactiveDays = 45;
    private const int LookbackDays = 165;
    private const int MinimumHistoricalDocuments = 2;

    public async Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        var referenceDate = context.ReferenceTime;
        var inactiveStart = referenceDate.AddDays(-InactiveDays);
        var lookbackStart = referenceDate.AddDays(-LookbackDays);
        var inactiveStartDateOnly = DateOnly.FromDateTime(inactiveStart);
        var lookbackStartDateOnly = DateOnly.FromDateTime(lookbackStart);
        var referenceDateOnly = DateOnly.FromDateTime(referenceDate);

        var sales = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(d => d.MovementCategory == FiscalMovementCategory.Sale
                && d.IssueDate >= lookbackStartDateOnly
                && d.IssueDate <= referenceDateOnly)
            .Include(d => d.Items)
            .ToListAsync(cancellationToken);

        var customerActivity = sales
            .GroupBy(d => d.CustomerId)
            .Where(g => g.Key.HasValue)
            .Select(g => new
            {
                CustomerId = g.Key!.Value,
                LastPurchase = g.Max(d => d.IssueDate),
                RecentDocs = g.Count(d => d.IssueDate >= inactiveStartDateOnly),
                HistoricalDocs = g.Count(d =>
                    d.IssueDate >= lookbackStartDateOnly
                    && d.IssueDate < inactiveStartDateOnly),
                Name = g.OrderByDescending(d => d.IssueDate).First().CustomerNameAtIssue,
                Code = g.First().CustomerCodeAtIssue,
                HistoricalTotal = g
                    .Where(d => d.IssueDate >= lookbackStartDateOnly
                        && d.IssueDate < inactiveStartDateOnly)
                    .SelectMany(d => d.Items)
                    .Sum(i => (i.UnitValue ?? 0m) * i.Quantity)
            })
            .ToList();

        var customerIds = customerActivity.Select(c => c.CustomerId).ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(c => c.Id);

        var analyzedCustomers = customers.Count;
        var findings = new List<FindingCandidate>();
        var now = DateTime.UtcNow;

        foreach (var activity in customerActivity)
        {
            if (customerMap.TryGetValue(activity.CustomerId, out var customer))
            {
                customer.LastPurchaseAt = new DateTime(
                    activity.LastPurchase.Year,
                    activity.LastPurchase.Month,
                    activity.LastPurchase.Day,
                    0, 0, 0, DateTimeKind.Utc);

                if (activity.RecentDocs > 0)
                {
                    customer.IsActive = true;
                    continue;
                }

                customer.IsActive = false;
            }

            if (activity.HistoricalDocs < MinimumHistoricalDocuments)
                continue;

            var daysSinceLastPurchase = (int)(referenceDateOnly.DayNumber - activity.LastPurchase.DayNumber);
            var historicalMonths = (decimal)LookbackDays / 30;
            var historicalMonthlyAvg = historicalMonths > 0
                ? activity.HistoricalTotal / historicalMonths
                : 0;

            findings.Add(new FindingCandidate(
                Fingerprint: $"{Code}:CUSTOMER:{activity.CustomerId}:{activity.Code}",
                Title: "Cliente inativo",
                Description: $"O cliente não realiza compras há {daysSinceLastPurchase} dias.",
                SubjectType: "Customer",
                SubjectId: activity.CustomerId.ToString(),
                SubjectLabel: activity.Name,
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new(
                        Name: "Dias sem compras",
                        Value: daysSinceLastPurchase.ToString(),
                        ReferenceValue: InactiveDays.ToString(),
                        Unit: "dias",
                        Description: $"Tempo desde a última compra (limite de inatividade: {InactiveDays} dias)",
                        SourceType: "FiscalDocument",
                        SourceId: activity.CustomerId.ToString(),
                        ObservedAt: referenceDate),
                    new(
                        Name: "Última compra",
                        Value: activity.LastPurchase.ToString("dd/MM/yyyy"),
                        ReferenceValue: null,
                        Unit: null,
                        Description: "Data da última venda registrada",
                        SourceType: "FiscalDocument",
                        SourceId: activity.CustomerId.ToString(),
                        ObservedAt: referenceDate),
                    new(
                        Name: "Média mensal histórica",
                        Value: historicalMonthlyAvg.ToString("F2"),
                        ReferenceValue: null,
                        Unit: "BRL",
                        Description: "Média mensal de compras antes da inatividade",
                        SourceType: "FiscalDocument",
                        SourceId: activity.CustomerId.ToString(),
                        ObservedAt: referenceDate),
                    new(
                        Name: "Documentos históricos",
                        Value: activity.HistoricalDocs.ToString(),
                        ReferenceValue: null,
                        Unit: "documentos",
                        Description: "Total de documentos de venda no período histórico",
                        SourceType: "FiscalDocument",
                        SourceId: activity.CustomerId.ToString(),
                        ObservedAt: referenceDate)
                }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DetectionResult(
            AnalyzedItems: analyzedCustomers,
            Findings: findings.AsReadOnly());
    }
}
