using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class CustomerPurchaseDropDetector(
    ImportDbContext dbContext) : IDetector
{
    public string Code => DetectorCodes.CustomerPurchaseDrop;

    private const int RecentDays = 30;
    private const int HistoricalDays = 60;
    private const decimal DropThresholdPercent = 50m;
    private const int MinimumHistoricalDocuments = 2;

    public async Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        var referenceDate = context.ReferenceTime;
        var recentStart = referenceDate.AddDays(-RecentDays);
        var historicalStart = referenceDate.AddDays(-RecentDays - HistoricalDays);
        var recentStartDateOnly = DateOnly.FromDateTime(recentStart);
        var historicalStartDateOnly = DateOnly.FromDateTime(historicalStart);
        var referenceDateOnly = DateOnly.FromDateTime(referenceDate);

        var activeCustomerIds = await dbContext.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var sales = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(d => d.MovementCategory == FiscalMovementCategory.Sale
                && d.CustomerId.HasValue
                && activeCustomerIds.Contains(d.CustomerId.Value)
                && d.IssueDate >= historicalStartDateOnly
                && d.IssueDate <= referenceDateOnly)
            .Include(d => d.Items)
            .ToListAsync(cancellationToken);

        var customerGroups = sales
            .GroupBy(d => new
            {
                CustomerId = d.CustomerId ?? Guid.Empty,
                d.CustomerCodeAtIssue
            })
            .ToList();

        var analyzedCustomers = activeCustomerIds.Count;
        var findings = new List<FindingCandidate>();

        foreach (var group in customerGroups)
        {
            var recentDocs = group
                .Where(d => d.IssueDate >= recentStartDateOnly)
                .ToList();

            var historicalDocs = group
                .Where(d => d.IssueDate >= historicalStartDateOnly
                    && d.IssueDate < recentStartDateOnly)
                .ToList();

            if (historicalDocs.Count < MinimumHistoricalDocuments)
                continue;

            var recentTotal = recentDocs
                .SelectMany(d => d.Items)
                .Sum(i => (i.UnitValue ?? 0m) * i.Quantity);

            var historicalTotal = historicalDocs
                .SelectMany(d => d.Items)
                .Sum(i => (i.UnitValue ?? 0m) * i.Quantity);

            var historicalMonths = (decimal)HistoricalDays / 30;
            var historicalMonthlyAvg = historicalMonths > 0
                ? historicalTotal / historicalMonths
                : 0;

            if (historicalMonthlyAvg <= 0)
                continue;

            var dropPercent = (int)((historicalMonthlyAvg - recentTotal) / historicalMonthlyAvg * 100);

            if (dropPercent < (int)DropThresholdPercent)
                continue;

            var subjectId = group.Key.CustomerId != Guid.Empty
                ? group.Key.CustomerId.ToString()
                : group.Key.CustomerCodeAtIssue;

            var customerName = group.OrderByDescending(d => d.IssueDate).First().CustomerNameAtIssue;

            findings.Add(new FindingCandidate(
                Fingerprint: $"{Code}:CUSTOMER:{subjectId}:{group.Key.CustomerCodeAtIssue}",
                Title: "Cliente fora do padrão de compra",
                Description: $"O cliente comprou {dropPercent}% abaixo da média histórica mensal nos últimos {RecentDays} dias.",
                SubjectType: "Customer",
                SubjectId: subjectId,
                SubjectLabel: customerName,
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new(
                        Name: "Compra nos últimos 30 dias",
                        Value: recentTotal.ToString("F2"),
                        ReferenceValue: historicalMonthlyAvg.ToString("F2"),
                        Unit: "BRL",
                        Description: $"Valor total de compras nos últimos {RecentDays} dias",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Média mensal histórica",
                        Value: historicalMonthlyAvg.ToString("F2"),
                        ReferenceValue: null,
                        Unit: "BRL",
                        Description: $"Média mensal dos últimos {HistoricalDays + RecentDays} dias",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Variação",
                        Value: $"-{dropPercent}",
                        ReferenceValue: null,
                        Unit: "%",
                        Description: "Queda percentual em relação à média histórica mensal",
                        SourceType: null,
                        SourceId: null,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Documentos recentes",
                        Value: recentDocs.Count.ToString(),
                        ReferenceValue: historicalDocs.Count.ToString(),
                        Unit: "documentos",
                        Description: $"Documentos de venda nos últimos {RecentDays} dias vs período histórico",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate)
                }));
        }

        return new DetectionResult(
            AnalyzedItems: analyzedCustomers,
            Findings: findings.AsReadOnly());
    }
}
