using InovaSkill.Importer.Application.Detection;
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

        var customerGroups = sales
            .GroupBy(d => new
            {
                CustomerId = d.CustomerId ?? Guid.Empty,
                d.CustomerCodeAtIssue
            })
            .ToList();

        var analyzedCustomers = await dbContext.Customers.CountAsync(cancellationToken);

        var findings = new List<FindingCandidate>();

        foreach (var group in customerGroups)
        {
            var recentDocs = group
                .Where(d => d.IssueDate >= inactiveStartDateOnly)
                .ToList();

            if (recentDocs.Count > 0)
                continue;

            var historicalDocs = group
                .Where(d => d.IssueDate >= lookbackStartDateOnly
                    && d.IssueDate < inactiveStartDateOnly)
                .ToList();

            if (historicalDocs.Count < MinimumHistoricalDocuments)
                continue;

            var lastPurchase = historicalDocs.Max(d => d.IssueDate);
            var daysSinceLastPurchase = (int)(referenceDateOnly.DayNumber - lastPurchase.DayNumber);

            var historicalTotal = historicalDocs
                .SelectMany(d => d.Items)
                .Sum(i => (i.UnitValue ?? 0m) * i.Quantity);

            var historicalMonths = (decimal)LookbackDays / 30;
            var historicalMonthlyAvg = historicalMonths > 0
                ? historicalTotal / historicalMonths
                : 0;

            var subjectId = group.Key.CustomerId != Guid.Empty
                ? group.Key.CustomerId.ToString()
                : group.Key.CustomerCodeAtIssue;

            var customerName = group.OrderByDescending(d => d.IssueDate).First().CustomerNameAtIssue;

            var fingerprint = $"{Code}:CUSTOMER:{subjectId}:{group.Key.CustomerCodeAtIssue}";

            findings.Add(new FindingCandidate(
                Fingerprint: fingerprint,
                Title: "Cliente inativo",
                Description: $"O cliente não realiza compras há {daysSinceLastPurchase} dias.",
                SubjectType: "Customer",
                SubjectId: subjectId,
                SubjectLabel: customerName,
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new(
                        Name: "Dias sem compras",
                        Value: daysSinceLastPurchase.ToString(),
                        ReferenceValue: InactiveDays.ToString(),
                        Unit: "dias",
                        Description: $"Tempo desde a última compra (limite de inatividade: {InactiveDays} dias)",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Última compra",
                        Value: lastPurchase.ToString("dd/MM/yyyy"),
                        ReferenceValue: null,
                        Unit: null,
                        Description: "Data da última venda registrada",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Média mensal histórica",
                        Value: historicalMonthlyAvg.ToString("F2"),
                        ReferenceValue: null,
                        Unit: "BRL",
                        Description: "Média mensal de compras antes da inatividade",
                        SourceType: "FiscalDocument",
                        SourceId: subjectId,
                        ObservedAt: referenceDate),
                    new(
                        Name: "Documentos históricos",
                        Value: historicalDocs.Count.ToString(),
                        ReferenceValue: null,
                        Unit: "documentos",
                        Description: "Total de documentos de venda no período histórico",
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
