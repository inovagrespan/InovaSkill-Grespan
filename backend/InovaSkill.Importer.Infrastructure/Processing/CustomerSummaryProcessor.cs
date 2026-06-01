using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class CustomerSummaryProcessor(
    ImportDbContext dbContext,
    ILogger<CustomerSummaryProcessor> logger) : IPostImportProcessor
{
    public PostImportJobType JobType => PostImportJobType.CustomerSummary;

    public async Task ProcessAsync(long fileJobId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting customer summary processing for file job {FileJobId}.", fileJobId);
        var step = new ProcessingStepExecution
        {
            FileJobId = fileJobId,
            Step = "SUMMARY",
            Status = "running",
            StartedAt = DateTime.UtcNow
        };
        dbContext.ProcessingStepExecutions.Add(step);
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = fileJobId,
            Stage = "SUMMARY",
            Level = "Information",
            Message = "Resumo de clientes iniciado."
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var sourceRows = await dbContext.CommercialTransactions
            .AsNoTracking()
            .Where(x => x.SourceFileJobId == fileJobId)
            .Select(x => new
            {
                x.TransactionDate,
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.ProductGroup,
                x.TransactionType,
                x.DocumentNumber,
                x.TotalAmount,
                x.Quantity,
                x.GrossWeightKg
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var daily = sourceRows
            .GroupBy(x => new
            {
                Date = ToUtcDate(x.TransactionDate),
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.ProductGroup,
                x.TransactionType
            })
            .Select(g => new CustomerSummaryDaily
            {
                SourceFileJobId = fileJobId,
                ReferenceDate = g.Key.Date,
                CustomerCode = g.Key.CustomerCode,
                CustomerName = g.Key.CustomerName,
                City = g.Key.City,
                ProductGroup = g.Key.ProductGroup,
                TransactionType = g.Key.TransactionType,
                Orders = g.Select(x => x.DocumentNumber).Distinct().Count(),
                Revenue = g.Sum(x => x.TotalAmount),
                Quantity = g.Sum(x => x.Quantity),
                Weight = g.Sum(x => x.GrossWeightKg),
                ProcessedAt = now
            })
            .ToList();

        var weekly = daily
            .GroupBy(x => new
            {
                WeekStart = GetWeekStartDate(x.ReferenceDate),
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.ProductGroup,
                x.TransactionType
            })
            .Select(g => new CustomerSummaryWeekly
            {
                SourceFileJobId = fileJobId,
                WeekStartDate = g.Key.WeekStart,
                CustomerCode = g.Key.CustomerCode,
                CustomerName = g.Key.CustomerName,
                City = g.Key.City,
                ProductGroup = g.Key.ProductGroup,
                TransactionType = g.Key.TransactionType,
                Orders = g.Sum(x => x.Orders),
                Revenue = g.Sum(x => x.Revenue),
                Quantity = g.Sum(x => x.Quantity),
                Weight = g.Sum(x => x.Weight),
                ProcessedAt = now
            })
            .ToList();

        var monthly = daily
            .GroupBy(x => new
            {
                MonthStart = ToUtcMonthStart(x.ReferenceDate),
                x.CustomerCode,
                x.CustomerName,
                x.City,
                x.ProductGroup,
                x.TransactionType
            })
            .Select(g => new CustomerSummaryMonthly
            {
                SourceFileJobId = fileJobId,
                MonthStartDate = g.Key.MonthStart,
                CustomerCode = g.Key.CustomerCode,
                CustomerName = g.Key.CustomerName,
                City = g.Key.City,
                ProductGroup = g.Key.ProductGroup,
                TransactionType = g.Key.TransactionType,
                Orders = g.Sum(x => x.Orders),
                Revenue = g.Sum(x => x.Revenue),
                Quantity = g.Sum(x => x.Quantity),
                Weight = g.Sum(x => x.Weight),
                ProcessedAt = now
            })
            .ToList();

        await dbContext.CustomerSummariesDaily.Where(x => x.SourceFileJobId == fileJobId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CustomerSummariesWeekly.Where(x => x.SourceFileJobId == fileJobId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CustomerSummariesMonthly.Where(x => x.SourceFileJobId == fileJobId).ExecuteDeleteAsync(cancellationToken);

        if (daily.Count > 0)
        {
            await dbContext.CustomerSummariesDaily.AddRangeAsync(daily, cancellationToken);
        }

        if (weekly.Count > 0)
        {
            await dbContext.CustomerSummariesWeekly.AddRangeAsync(weekly, cancellationToken);
        }

        if (monthly.Count > 0)
        {
            await dbContext.CustomerSummariesMonthly.AddRangeAsync(monthly, cancellationToken);
        }

        if (daily.Count > 0 || weekly.Count > 0 || monthly.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        step.Status = "completed";
        step.FinishedAt = DateTime.UtcNow;
        step.ProcessedRows = sourceRows.Count;
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = fileJobId,
            Stage = "SUMMARY",
            Level = "Information",
            Message = $"Resumo de clientes concluido com {daily.Count} diario(s), {weekly.Count} semanal(is) e {monthly.Count} mensal(is)."
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Finished customer summary processing for file job {FileJobId}. Generated {DailyCount} daily, {WeeklyCount} weekly and {MonthlyCount} monthly rows.",
            fileJobId,
            daily.Count,
            weekly.Count,
            monthly.Count);
    }

    private static DateTime GetWeekStartDate(DateTime referenceDate)
    {
        var date = referenceDate.Date;
        var diff = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return DateTime.SpecifyKind(date.AddDays(diff), DateTimeKind.Utc);
    }

    private static DateTime ToUtcDate(DateTime input)
    {
        return DateTime.SpecifyKind(input.Date, DateTimeKind.Utc);
    }

    private static DateTime ToUtcMonthStart(DateTime input)
    {
        return DateTime.SpecifyKind(new DateTime(input.Year, input.Month, 1), DateTimeKind.Utc);
    }
}
