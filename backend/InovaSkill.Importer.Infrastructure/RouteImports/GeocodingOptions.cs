namespace InovaSkill.Importer.Infrastructure.RouteImports;

public static class GeocodingProviders
{
    public const string Google = "Google";
    public const string Geoapify = "Geoapify";
    public const string Nominatim = "Nominatim";
}

public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";
    public string Provider { get; set; } = GeocodingProviders.Geoapify;
}

public sealed class GeoapifyOptions
{
    public const string SectionName = "Geoapify";
    public string BaseUrl { get; set; } = "https://api.geoapify.com/";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int TransportMaximumRetries { get; set; } = 3;
    public int TransportRetryDelayMilliseconds { get; set; } = 500;
    public int MinimumRequestIntervalMilliseconds { get; set; } = 250;
    public int RateLimitMaximumRetries { get; set; } = 4;
    public int RateLimitFallbackDelaySeconds { get; set; } = 5;
    public int RateLimitMaximumDelaySeconds { get; set; } = 60;
    public decimal MaximumSimulationDistanceMeters { get; set; } = 25_000m;
}
