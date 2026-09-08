namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";
    public const int DefaultTimeoutSeconds = 30;
    public const int DefaultMaximumWaypoints = 50;

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org/";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
    public int MaximumWaypoints { get; set; } = DefaultMaximumWaypoints;
}
