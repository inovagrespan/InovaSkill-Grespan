namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmOptions
{
    public const string SectionName = "Osrm";
    public const int DefaultTimeoutSeconds = 30;
    public const int DefaultMatrixBlockSize = 50;
    public const int DefaultMaximumParallelRequests = 2;

    public string BaseUrl { get; set; } = "http://localhost:5000";
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
    public int MatrixBlockSize { get; set; } = DefaultMatrixBlockSize;
    public int MaximumParallelRequests { get; set; } = DefaultMaximumParallelRequests;
}
