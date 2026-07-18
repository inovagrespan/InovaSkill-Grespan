namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

public sealed class ImportHangfireOptions
{
    public const string SectionName = "Hangfire";

    public bool Enabled { get; set; } = true;

    public HangfireStorageOptions Storage { get; set; } = new();

    public HangfireDashboardOptions Dashboard { get; set; } = new();

    public HangfireWorkerOptions Workers { get; set; } = new();
}

public sealed class HangfireStorageOptions
{
    public string? ConnectionString { get; set; }

    public string SchemaName { get; set; } = "hangfire";
}

public sealed class HangfireDashboardOptions
{
    public bool Enabled { get; set; } = true;

    public string Path { get; set; } = "/hangfire";

    public bool AllowAnonymous { get; set; }
}

public sealed class HangfireWorkerOptions
{
    public int Imports { get; set; } = 2;

    public int Detectors { get; set; } = 2;

    public int Default { get; set; } = 2;
}
