namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteOptimizationOptions
{
    public const string SectionName = "RouteOptimization";

    public string DistanceProvider { get; set; } = RouteDistanceProviderNames.Geographic;
    public string OsrmBaseUrl { get; set; } = "http://localhost:5000";
    public int OsrmTimeoutSeconds { get; set; } = 10;
    public int MaximumMatrixPoints { get; set; } = 100;
}

public static class RouteDistanceProviderNames
{
    public const string Geographic = "Geographic";
    public const string Osrm = "Osrm";
}
