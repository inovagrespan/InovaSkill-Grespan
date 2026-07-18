namespace InovaSkill.Importer.Application.RouteImports;

public interface IRouteChatQueryService
{
    Task<IReadOnlyList<RouteChatSummaryDto>> SearchRoutesAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken);

    Task<RouteChatDetailsDto?> GetRouteDetailsAsync(
        Guid routeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RouteChatCriticalDto>> GetCriticalRoutesAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RouteChatSummaryDto>> ListRoutesByOccupancyAsync(
        RouteChatOccupancyQuery query,
        CancellationToken cancellationToken);

    Task<RouteChatCitiesDto?> GetRouteCitiesAsync(
        Guid routeId,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record RouteChatOccupancyQuery(
    string? OccupancyLevel,
    decimal? MinimumOccupancyPercentage,
    decimal? MaximumOccupancyPercentage,
    string SortDirection,
    int Limit);

public sealed record RouteChatSummaryDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage);

public sealed record RouteChatDetailsDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage,
    int CityCount,
    int CustomerCount,
    DateTime UpdatedAt);

public sealed record RouteChatCriticalDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage,
    string Reason);

public sealed record RouteChatCitiesDto(
    Guid RouteId,
    string RouteName,
    IReadOnlyList<RouteChatCityDto> Cities);

public sealed record RouteChatCityDto(string Name, string? State);
