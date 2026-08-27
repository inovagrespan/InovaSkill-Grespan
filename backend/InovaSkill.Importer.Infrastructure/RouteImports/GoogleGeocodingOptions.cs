namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class GoogleGeocodingOptions
{
    public const string SectionName = "GoogleGeocoding";
    public string BaseUrl { get; set; } = "https://geocode.googleapis.com/v4/";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MinimumRequestIntervalMilliseconds { get; set; } = 500;
    public int RateLimitMaximumRetries { get; set; } = 4;
    public int RateLimitFallbackDelaySeconds { get; set; } = 5;
    public int RateLimitMaximumDelaySeconds { get; set; } = 60;
}
