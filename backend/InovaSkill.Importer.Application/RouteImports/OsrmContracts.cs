namespace InovaSkill.Importer.Application.RouteImports;

public static class OsrmMatrixPointTypes
{
    public const string Depot = "Depot";
    public const string Municipality = "Municipality";
}

public sealed record OsrmMatrixPoint(
    Guid Id,
    string Type,
    decimal Latitude,
    decimal Longitude);

public sealed record OsrmTableRequest(
    string Weekday,
    IReadOnlyList<OsrmMatrixPoint> Points);

public sealed record OsrmTableResult(
    string Source,
    IReadOnlyList<OsrmMatrixPoint> Points,
    IReadOnlyList<IReadOnlyList<decimal>> DurationsSeconds,
    IReadOnlyList<IReadOnlyList<decimal>> DistancesMeters);

public interface IOsrmTableClient
{
    Task<OsrmTableResult> GetTableAsync(
        OsrmTableRequest request,
        CancellationToken cancellationToken);

    Task<bool> IsHealthyAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken);
}

public interface IOsrmDailyMatrixService
{
    Task<OsrmTableResult> GetForDayAsync(
        Guid routeImportId,
        string weekday,
        CancellationToken cancellationToken);
}

public sealed class OsrmTableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
