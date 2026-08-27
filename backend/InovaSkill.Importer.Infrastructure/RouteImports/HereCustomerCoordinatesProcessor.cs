using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class HereCustomerCoordinatesProcessor(
    ImportDbContext db,
    IImportFileStorage fileStorage,
    HereCustomerCoordinatesCsvParser parser) : IDataSourceProcessor
{
    public string SourceCode => HereCustomerCoordinateImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var import = await db.RouteImports.SingleAsync(item => item.Id == importId, cancellationToken);
        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content);
        var currentCustomerImportId = await db.DataSources.AsNoTracking()
            .Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentCustomerImportId.HasValue)
            throw new StructuralImportException("Importe o cadastro de clientes antes das coordenadas HERE.");

        var snapshots = await db.CustomerSnapshots
            .Include(snapshot => snapshot.Customer).ThenInclude(customer => customer!.RegistrationAddress)
            .Where(snapshot => snapshot.ImportId == currentCustomerImportId)
            .ToListAsync(cancellationToken);
        var byCode = snapshots.GroupBy(snapshot => NormalizeCustomerCode(snapshot.Customer!.ExternalCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        foreach (var row in parsed.Rows)
        {
            if (!byCode.TryGetValue(NormalizeCustomerCode(row.ExternalCode), out var matches) || matches.Length == 0)
                throw new StructuralImportException($"Linha {row.RowNumber}: cliente TOTVS {row.ExternalCode} não encontrado no snapshot atual.");
            if (matches.Length > 1)
                throw new StructuralImportException($"Linha {row.RowNumber}: cliente TOTVS {row.ExternalCode} possui mais de uma loja; informe um identificador de loja.");
            var customer = matches[0].Customer!;
            var address = customer.RegistrationAddress;
            if (address is null)
            {
                address = CreateAddress(customer.Id, row, now);
                db.CustomerRegistrationAddresses.Add(address);
                customer.RegistrationAddress = address;
            }
            var coordinate = await db.CustomerAddressCoordinates
                .SingleOrDefaultAsync(item => item.CustomerRegistrationAddressId == address.Id, cancellationToken);
            var isNewCoordinate = coordinate is null;
            coordinate ??= new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(),
                CustomerRegistrationAddressId = address.Id,
                CreatedAt = now
            };
            coordinate.NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address);
            coordinate.Source = "HERE_IMPORT";
            coordinate.Status = CustomerAddressCoordinateStatuses.Resolved;
            coordinate.Precision = row.Status == "NUMERO EXATO"
                ? CustomerAddressCoordinatePrecisions.Exact
                : CustomerAddressCoordinatePrecisions.Interpolated;
            coordinate.Latitude = row.Latitude;
            coordinate.Longitude = row.Longitude;
            coordinate.DisplayName = row.DisplayName;
            coordinate.FailureReason = null;
            coordinate.LastAttemptAt = now;
            coordinate.ResolvedAt = now;
            coordinate.UpdatedAt = now;
            if (isNewCoordinate)
                db.CustomerAddressCoordinates.Add(coordinate);
        }
        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = parsed.Rows.Count;
        import.ErrorCount = parsed.IgnoredRows;
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static CustomerRegistrationAddress CreateAddress(Guid customerId, ParsedHereCustomerCoordinateRow row, DateTime now)
    {
        var address = row.OriginalAddress.Trim();
        var number = string.Empty;
        var street = address;
        var comma = address.LastIndexOf(',');
        if (comma >= 0) { street = address[..comma].Trim(); number = address[(comma + 1)..].Trim(); }
        string? streetType = null;
        if (street.StartsWith("AV ", StringComparison.OrdinalIgnoreCase)) { streetType = "AVENIDA"; street = street[3..].Trim(); }
        else if (street.StartsWith("AVENIDA ", StringComparison.OrdinalIgnoreCase)) { streetType = "AVENIDA"; street = street[8..].Trim(); }
        else if (street.StartsWith("R ", StringComparison.OrdinalIgnoreCase)) { streetType = "RUA"; street = street[2..].Trim(); }
        else if (street.StartsWith("RUA ", StringComparison.OrdinalIgnoreCase)) { streetType = "RUA"; street = street[4..].Trim(); }
        return new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customerId, Source = "HERE_IMPORT",
            Status = CustomerRegistrationAddressStatuses.Resolved, City = row.City,
            StateCode = ResolveStateCode(row.State), PostalCode = row.PostalCode,
            StreetType = streetType, Street = street, Number = number,
            ResolvedAt = now, LastAttemptAt = now, CreatedAt = now, UpdatedAt = now
        };
    }

    private static string? ResolveStateCode(string state) => state.Trim().ToUpperInvariant() switch
    {
        "SÃO PAULO" => "SP", "MATO GROSSO DO SUL" => "MS", "MINAS GERAIS" => "MG",
        "PARANÁ" => "PR", var value when value.Length == 2 => value, _ => null
    };

    private static string NormalizeCustomerCode(string value)
    {
        var compact = value.Trim().ToUpperInvariant();
        if (!compact.All(char.IsDigit)) return compact;
        var normalized = compact.TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }
}
