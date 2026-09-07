namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public string UserAgent { get; set; } = "InovaSkill-Grespan/1.0";
    public int MinimumRequestIntervalMilliseconds { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
    public int PersistenceBatchSize { get; set; } = 25;
    public int TransportMaximumRetries { get; set; } = 3;
    public int TransportRetryDelayMilliseconds { get; set; } = 500;
}
