using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class SalesSummaryProcessor(
    ImportDbContext dbContext,
    ILogger<SalesSummaryProcessor> logger) : IPostImportProcessor
{
    public PostImportJobType JobType => PostImportJobType.SalesSummary;

    public async Task ProcessAsync(long fileJobId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting sales summary processing for file job {FileJobId}.", fileJobId);

        var sourceQuery = dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.SourceFileJobId == fileJobId);

        var analyzedCount = await sourceQuery.CountAsync(cancellationToken);

        var sourceRows = await sourceQuery
            .Select(x => new
            {
                x.TransactionDate,
                x.City,
                x.ProductGroup,
                x.TransactionType,
                x.Quantity,
                x.TotalAmount,
                x.GrossWeightKg
            })
            .ToListAsync(cancellationToken);

        var dailySummaries = sourceRows
            .GroupBy(x => new
            {
                Date = x.TransactionDate.Date,
                x.City,
                x.ProductGroup,
                x.TransactionType
            })
            .Select(g => new SalesSummaryDaily
            {
                SourceFileJobId = fileJobId,
                ReferenceDate = g.Key.Date,
                City = g.Key.City,
                ProductGroup = g.Key.ProductGroup,
                TransactionType = g.Key.TransactionType,
                TransactionCount = g.Count(),
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalAmount = g.Sum(x => x.TotalAmount),
                TotalGrossWeightKg = g.Sum(x => x.GrossWeightKg),
                ProcessedAt = DateTime.UtcNow
            })
            .ToList();

        var weeklySummaries = dailySummaries
            .GroupBy(x => new
            {
                WeekStartDate = GetWeekStartDate(x.ReferenceDate),
                x.City,
                x.ProductGroup,
                x.TransactionType
            })
            .Select(g => new SalesSummaryWeekly
            {
                SourceFileJobId = fileJobId,
                WeekStartDate = g.Key.WeekStartDate,
                City = g.Key.City,
                ProductGroup = g.Key.ProductGroup,
                TransactionType = g.Key.TransactionType,
                TransactionCount = g.Sum(x => x.TransactionCount),
                TotalQuantity = g.Sum(x => x.TotalQuantity),
                TotalAmount = g.Sum(x => x.TotalAmount),
                TotalGrossWeightKg = g.Sum(x => x.TotalGrossWeightKg),
                ProcessedAt = DateTime.UtcNow
            })
            .ToList();

        await dbContext.SalesSummariesDaily
            .Where(x => x.SourceFileJobId == fileJobId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.SalesSummariesWeekly
            .Where(x => x.SourceFileJobId == fileJobId)
            .ExecuteDeleteAsync(cancellationToken);

        if (dailySummaries.Count > 0)
        {
            await dbContext.SalesSummariesDaily.AddRangeAsync(dailySummaries, cancellationToken);
        }

        if (weeklySummaries.Count > 0)
        {
            await dbContext.SalesSummariesWeekly.AddRangeAsync(weeklySummaries, cancellationToken);
        }

        if (dailySummaries.Count > 0 || weeklySummaries.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Finished sales summary processing for file job {FileJobId}. Analyzed {AnalyzedCount} rows and generated {DailyCount} daily summaries and {WeeklyCount} weekly summaries.",
            fileJobId,
            analyzedCount,
            dailySummaries.Count,
            weeklySummaries.Count);
    }

    private static DateTime GetWeekStartDate(DateTime referenceDate)
    {
        var date = referenceDate.Date;
        var diff = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(diff);
    }
}
