using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class CustomerRouteAssignmentsProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    CustomerRouteAssignmentsSpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => CustomerRouteAssignmentImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(importId.ToByteArray(), 0)})", cancellationToken);

        var import = await dbContext.RouteImports.SingleAsync(x => x.Id == importId, cancellationToken);
        var corrections = await dbContext.RouteImportErrors.AsNoTracking()
            .Where(x => x.ImportId == importId && x.Status == ImportErrorStatus.Resolved && x.CorrectedValue != null)
            .ToDictionaryAsync(x => new ValueTuple<string, int, string>(x.SheetName, x.RowNumber, x.Field),
                x => x.CorrectedValue!, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content, corrections);

        await dbContext.CustomerRouteMappings.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.RouteImportErrors.Where(x => x.ImportId == importId && x.Status == ImportErrorStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);

        var customerImportId = await CurrentImportId(CustomerImportCodes.DataSource, cancellationToken);
        var routeImportId = await CurrentImportId(RouteImportCodes.DataSource, cancellationToken);
        var customers = customerImportId.HasValue
            ? await dbContext.CustomerSnapshots.AsNoTracking().Where(x => x.ImportId == customerImportId)
                .Select(x => new CustomerCandidate(x.CustomerId, x.TradeName, x.LegalName,
                    x.Municipality!.NormalizedName)).ToListAsync(cancellationToken) : [];
        var routes = routeImportId.HasValue
            ? await dbContext.Routes.AsNoTracking().Where(x => x.ImportId == routeImportId)
                .Select(x => new RouteCandidate(x.Id, x.Name, x.Weekday)).ToListAsync(cancellationToken) : [];
        var now = DateTime.UtcNow;
        var imported = 0;
        foreach (var row in parsed.Rows)
        {
            var invalid = false;
            invalid |= AddRequiredError(importId, row, "weekday", row.Weekday,
                CustomerRouteAssignmentsSpreadsheetParser.IsSupportedWeekday(row.Weekday), "Dia da semana inválido.", now);
            invalid |= AddRequiredError(importId, row, "market_name", row.MarketName,
                !string.IsNullOrWhiteSpace(row.MarketName), "Mercado é obrigatório.", now);
            invalid |= AddRequiredError(importId, row, "route_name", row.RouteName,
                !string.IsNullOrWhiteSpace(row.RouteName), "Rota é obrigatória.", now);
            invalid |= AddRequiredError(importId, row, "municipality_name", row.MunicipalityName,
                !string.IsNullOrWhiteSpace(row.MunicipalityName), "Cidade é obrigatória.", now);
            if (invalid) continue;

            var customerId = ResolveCorrectedGuid(corrections, row, CustomerRouteAssignmentImportCodes.CustomerCorrectionField);
            if (!customerId.HasValue)
            {
                var market = CustomerRouteAssignmentsSpreadsheetParser.Normalize(row.MarketName);
                var city = CustomerRouteAssignmentsSpreadsheetParser.Normalize(row.MunicipalityName);
                var matches = customers.Where(x =>
                    CustomerRouteAssignmentsSpreadsheetParser.Normalize(x.Municipality) == city &&
                    (CustomerRouteAssignmentsSpreadsheetParser.Normalize(x.TradeName) == market ||
                     CustomerRouteAssignmentsSpreadsheetParser.Normalize(x.LegalName) == market))
                    .Select(x => x.Id).Distinct().ToArray();
                if (matches.Length == 1) customerId = matches[0];
                else AddResolutionError(importId, row, CustomerRouteAssignmentImportCodes.CustomerCorrectionField,
                    row.MarketName, matches.Length == 0 ? "Cliente não encontrado por nome e cidade." : "Mais de um cliente corresponde ao nome e cidade.", now);
            }
            else if (!customers.Any(x => x.Id == customerId.Value))
            {
                AddResolutionError(importId, row, CustomerRouteAssignmentImportCodes.CustomerCorrectionField,
                    row.MarketName, "O cliente corrigido não pertence ao snapshot atual.", now);
                customerId = null;
            }

            var correctedRouteId = ResolveCorrectedGuid(corrections, row, CustomerRouteAssignmentImportCodes.RouteCorrectionField);
            RouteCandidate? route = correctedRouteId.HasValue ? routes.SingleOrDefault(x => x.Id == correctedRouteId) : null;
            if (route is null)
            {
                var routeName = CustomerRouteAssignmentsSpreadsheetParser.Normalize(row.RouteName);
                var matches = routes.Where(x => x.Weekday == row.Weekday &&
                    CustomerRouteAssignmentsSpreadsheetParser.Normalize(x.Name) == routeName).ToArray();
                if (matches.Length == 1) route = matches[0];
                else AddResolutionError(importId, row, CustomerRouteAssignmentImportCodes.RouteCorrectionField,
                    row.RouteName, matches.Length == 0 ? "Rota não encontrada para o dia informado." : "Mais de uma rota corresponde ao nome e dia.", now);
            }
            if (!customerId.HasValue || route is null) continue;
            dbContext.CustomerRouteMappings.Add(new CustomerRouteMapping
            {
                Id = Guid.NewGuid(), ImportId = importId, SheetName = row.SheetName,
                SourceRowNumber = row.RowNumber, CustomerId = customerId.Value, Weekday = route.Weekday,
                RouteName = route.Name, NormalizedRouteName = CustomerRouteAssignmentsSpreadsheetParser.Normalize(route.Name),
                MarketName = row.MarketName, MunicipalityName = row.MunicipalityName, CreatedAt = now
            });
            imported++;
        }
        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = imported;
        import.ErrorCount = dbContext.ChangeTracker.Entries<RouteImportError>().Count(x => x.State == EntityState.Added);
        import.Status = import.ErrorCount > 0 ? RouteImportStatus.NeedsReview : RouteImportStatus.Completed;
        import.FinishedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private bool AddRequiredError(Guid importId, ParsedCustomerRouteAssignmentRow row, string field,
        string raw, bool valid, string message, DateTime now)
    {
        if (valid) return false;
        AddResolutionError(importId, row, field, raw, message, now);
        return true;
    }

    private void AddResolutionError(Guid importId, ParsedCustomerRouteAssignmentRow row,
        string field, string raw, string message, DateTime now) => dbContext.RouteImportErrors.Add(new RouteImportError
    {
        Id = Guid.NewGuid(), ImportId = importId, SheetName = row.SheetName, RowNumber = row.RowNumber,
        Field = field, RawValue = raw, Message = message, Status = ImportErrorStatus.Pending, CreatedAt = now
    });

    private static Guid? ResolveCorrectedGuid(IReadOnlyDictionary<(string, int, string), string> corrections,
        ParsedCustomerRouteAssignmentRow row, string field) =>
        corrections.TryGetValue((row.SheetName, row.RowNumber, field), out var value) && Guid.TryParse(value, out var id) ? id : null;

    private async Task<Guid?> CurrentImportId(string code, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking().Where(x => x.Code == code)
            .Select(x => x.CurrentImportId).SingleOrDefaultAsync(cancellationToken);

    private sealed record CustomerCandidate(Guid Id, string TradeName, string LegalName, string Municipality);
    private sealed record RouteCandidate(Guid Id, string Name, string Weekday);
}
