using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null, [FromQuery] string? state = null,
        [FromQuery] string? municipality = null, [FromQuery] string? customerType = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var currentImportId = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentImportId.HasValue)
        {
            return Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });
        }

        var query = dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == currentImportId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            var normalizedMunicipalityTerm = MunicipalityNameNormalizer.Normalize(search);
            query = query.Where(snapshot =>
                snapshot.Customer!.ExternalCode.ToUpper().Contains(term) ||
                snapshot.LegalName.ToUpper().Contains(term) ||
                snapshot.TradeName.ToUpper().Contains(term) ||
                snapshot.DocumentNumber.ToUpper().Contains(term) ||
                snapshot.Municipality!.NormalizedName.Contains(normalizedMunicipalityTerm));
        }
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(snapshot => snapshot.Municipality!.StateCode == state.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(municipality))
            query = query.Where(snapshot => snapshot.Municipality!.NormalizedName ==
                MunicipalityNameNormalizer.Normalize(municipality));
        if (!string.IsNullOrWhiteSpace(customerType))
            query = query.Where(snapshot => snapshot.CustomerType == customerType.Trim());

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(snapshot => snapshot.Customer!.ExternalCode)
            .ThenBy(snapshot => snapshot.Customer!.BranchCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(snapshot => new
            {
                id = snapshot.CustomerId,
                snapshot.Customer!.ExternalCode,
                snapshot.Customer.BranchCode,
                snapshot.DocumentNumber,
                snapshot.DocumentType,
                snapshot.LegalName,
                snapshot.TradeName,
                snapshot.CustomerType,
                snapshot.Municipality!.StateCode,
                municipalityName = snapshot.Municipality.Name
            }).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{id:guid}/consumption-summary")]
    public async Task<ActionResult> ConsumptionSummary(Guid id, [FromQuery] DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var reference = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var currentStart = reference.AddDays(-29);
        var previousStart = currentStart.AddDays(-30);
        var averageStart = reference.AddDays(-89);
        var timelineStart = new DateOnly(reference.Year, reference.Month, 1).AddMonths(-11);
        var customer = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(x => x.CustomerId == id && x.Import!.DataSource!.CurrentImportId == x.ImportId)
            .Select(x => new { id = x.CustomerId, x.Customer!.ExternalCode, x.Customer.BranchCode,
                x.LegalName, x.TradeName, x.DocumentNumber, x.DocumentType, x.CustomerType,
                x.Municipality!.StateCode, municipalityName = x.Municipality.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null) return NotFound();
        var sales = dbContext.FiscalDocumentItems.AsNoTracking().Where(x =>
            x.FiscalDocument!.CustomerId == id && x.FiscalDocument.MovementCategory == Domain.Enums.FiscalMovementCategory.Sale);
        var current = await sales.Where(x => x.FiscalDocument!.IssueDate >= currentStart &&
            x.FiscalDocument.IssueDate <= reference).SumAsync(x => x.GrossWeightKg, cancellationToken);
        var previous = await sales.Where(x => x.FiscalDocument!.IssueDate >= previousStart &&
            x.FiscalDocument.IssueDate < currentStart).SumAsync(x => x.GrossWeightKg, cancellationToken);
        var ninety = await sales.Where(x => x.FiscalDocument!.IssueDate >= averageStart &&
            x.FiscalDocument.IssueDate <= reference).SumAsync(x => x.GrossWeightKg, cancellationToken);
        var lastPurchase = await dbContext.FiscalDocuments.AsNoTracking().Where(x =>
            x.CustomerId == id && x.MovementCategory == Domain.Enums.FiscalMovementCategory.Sale)
            .MaxAsync(x => (DateOnly?)x.IssueDate, cancellationToken);
        var saleDocumentsLast30Days = await dbContext.FiscalDocuments.AsNoTracking().CountAsync(x =>
            x.CustomerId == id && x.MovementCategory == Domain.Enums.FiscalMovementCategory.Sale &&
            x.IssueDate >= currentStart && x.IssueDate <= reference, cancellationToken);
        var monthlyFacts = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(x => x.CustomerId == id && x.IssueDate >= timelineStart && x.IssueDate <= reference)
            .Select(x => new {
                x.IssueDate.Year,
                x.IssueDate.Month,
                x.MovementCategory,
                GrossWeightKg = x.Items.Sum(item => item.GrossWeightKg),
                CalculatedAmount = x.Items.Sum(item =>
                    item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0)
            })
            .GroupBy(x => new { x.Year, x.Month, x.MovementCategory })
            .Select(group => new {
                group.Key.Year,
                group.Key.Month,
                group.Key.MovementCategory,
                DocumentCount = group.Count(),
                GrossWeightKg = group.Sum(x => x.GrossWeightKg),
                CalculatedAmount = group.Sum(x => x.CalculatedAmount)
            })
            .ToListAsync(cancellationToken);
        var monthlyTimeline = Enumerable.Range(0, 12).Select(offset =>
        {
            var month = timelineStart.AddMonths(offset);
            var salesMonth = monthlyFacts.SingleOrDefault(x =>
                x.Year == month.Year && x.Month == month.Month &&
                x.MovementCategory == Domain.Enums.FiscalMovementCategory.Sale);
            var returnMonth = monthlyFacts.SingleOrDefault(x =>
                x.Year == month.Year && x.Month == month.Month &&
                x.MovementCategory == Domain.Enums.FiscalMovementCategory.Return);
            var bonusMonth = monthlyFacts.SingleOrDefault(x =>
                x.Year == month.Year && x.Month == month.Month &&
                x.MovementCategory == Domain.Enums.FiscalMovementCategory.Bonus);
            var salesWeight = salesMonth?.GrossWeightKg ?? 0;
            var salesDocuments = salesMonth?.DocumentCount ?? 0;
            return new {
                month = month.ToString("yyyy-MM"),
                salesWeightKg = salesWeight,
                salesDocumentCount = salesDocuments,
                averageSalesWeightPerDocumentKg = salesDocuments == 0
                    ? 0 : Math.Round(salesWeight / salesDocuments, 3),
                calculatedSalesAmount = salesMonth?.CalculatedAmount ?? 0,
                returnWeightKg = returnMonth?.GrossWeightKg ?? 0,
                bonusWeightKg = bonusMonth?.GrossWeightKg ?? 0
            };
        }).ToArray();
        var salesWeight12Months = monthlyTimeline.Sum(x => x.salesWeightKg);
        var salesDocuments12Months = monthlyTimeline.Sum(x => x.salesDocumentCount);
        var returnWeight12Months = monthlyTimeline.Sum(x => x.returnWeightKg);
        var bonusWeight12Months = monthlyTimeline.Sum(x => x.bonusWeightKg);
        var calculatedSalesAmount12Months = monthlyTimeline.Sum(x => x.calculatedSalesAmount);
        var movements = await dbContext.FiscalDocuments.AsNoTracking().Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.DocumentNumber).Take(10)
            .Select(x => new { x.Id, x.IssueDate, x.DocumentNumber, x.Series,
                operationCategory = x.MovementCategory.ToString(), x.OperationDescription,
                itemCount = x.Items.Count, grossWeightKg = x.Items.Sum(item => item.GrossWeightKg) })
            .ToListAsync(cancellationToken);
        return Ok(new { customer, metrics = new {
            salesWeightLast30Days = current, salesWeightPrevious30Days = previous,
            variationPercentage = previous == 0 ? (decimal?)null : Math.Round((current - previous) / previous * 100, 1),
            variationStatus = previous == 0 && current > 0 ? "NEW_ACTIVITY" : "COMPARABLE",
            averageMonthlySalesWeight90Days = Math.Round(ninety / 3, 3),
            averageMonthlySalesWeight12Months = Math.Round(salesWeight12Months / 12, 3),
            saleDocumentsLast30Days,
            averageSalesWeightPerDocument12Months = salesDocuments12Months == 0
                ? 0 : Math.Round(salesWeight12Months / salesDocuments12Months, 3),
            averageMonthlyCalculatedSalesAmount12Months = Math.Round(calculatedSalesAmount12Months / 12, 2),
            returnWeight12Months,
            bonusWeight12Months,
            lastPurchaseDate = lastPurchase
        }, monthlyTimeline, recentMovements = movements });
    }

    [HttpGet("{id:guid}/projection")]
    public async Task<ActionResult> Projection(Guid id, CancellationToken cancellationToken = default)
    {
        var customerExists = await dbContext.CustomerSnapshots.AsNoTracking().AnyAsync(
            snapshot => snapshot.CustomerId == id &&
                snapshot.Import!.DataSource!.CurrentImportId == snapshot.ImportId,
            cancellationToken);
        if (!customerExists) return NotFound();

        var latestFiscalDate = await dbContext.FiscalDocuments.AsNoTracking()
            .MaxAsync(document => (DateOnly?)document.IssueDate, cancellationToken);
        if (!latestFiscalDate.HasValue)
        {
            return Ok(new { available = false, reason = "NO_FISCAL_HISTORY" });
        }

        var baseEndMonth = new DateOnly(latestFiscalDate.Value.Year, latestFiscalDate.Value.Month, 1)
            .AddMonths(-1);
        var baseStartMonth = baseEndMonth.AddMonths(-(CustomerProjectionCalculator.HistoricalMonthCount - 1));
        var baseEndDate = baseEndMonth.AddMonths(1).AddDays(-1);
        var monthlyFacts = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(document => document.CustomerId == id &&
                document.MovementCategory == Domain.Enums.FiscalMovementCategory.Sale &&
                document.IssueDate >= baseStartMonth && document.IssueDate <= baseEndDate)
            .Select(document => new
            {
                document.IssueDate.Year,
                document.IssueDate.Month,
                SalesWeightKg = document.Items.Sum(item => item.GrossWeightKg),
                CalculatedSalesAmount = document.Items.Sum(item =>
                    item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0)
            })
            .GroupBy(item => new { item.Year, item.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                SalesWeightKg = group.Sum(item => item.SalesWeightKg),
                CalculatedSalesAmount = group.Sum(item => item.CalculatedSalesAmount)
            })
            .ToListAsync(cancellationToken);
        var observations = Enumerable.Range(0, CustomerProjectionCalculator.HistoricalMonthCount)
            .Select(offset =>
            {
                var month = baseStartMonth.AddMonths(offset);
                var fact = monthlyFacts.SingleOrDefault(item =>
                    item.Year == month.Year && item.Month == month.Month);
                return new CustomerMonthlyObservation(
                    month,
                    fact?.SalesWeightKg ?? 0,
                    fact?.CalculatedSalesAmount ?? 0);
            }).ToArray();
        var projection = CustomerProjectionCalculator.Calculate(observations);

        return Ok(new
        {
            available = true,
            sourceCoverageDate = latestFiscalDate,
            projection.BaseStartMonth,
            projection.BaseEndMonth,
            historical = observations,
            projection.Weight,
            projection.Revenue,
            methodology = new
            {
                model = "LINEAR_REGRESSION",
                historicalMonths = CustomerProjectionCalculator.HistoricalMonthCount,
                forecastMonths = CustomerProjectionCalculator.ForecastMonthCount,
                confidenceLevel = 0.95m,
                partialSourceMonthExcluded = true
            }
        });
    }
}
