namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class BrasilApiOptions
{
    public const string SectionName = "BrasilApi";
    public string BaseUrl { get; set; } = "https://brasilapi.com.br/api/";
    public int RequestsPerSecond { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 30;
    public string UserAgent { get; set; } = "InovaSkill-Grespan/1.0";
    public int RateLimitMaximumRetries { get; set; } = 4;
    public int RateLimitFallbackDelaySeconds { get; set; } = 60;
    public int RateLimitMaximumDelaySeconds { get; set; } = 480;
    public int RateLimitJitterMaximumMilliseconds { get; set; } = 1000;
    public int PersistenceBatchSize { get; set; } = 25;
}
