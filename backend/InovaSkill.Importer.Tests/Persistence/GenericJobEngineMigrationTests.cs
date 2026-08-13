using InovaSkill.Importer.Infrastructure.Persistence.Migrations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class GenericJobEngineMigrationTests
{
    [Fact]
    public void Migration_DefinesVersionedJsonSchedulingRetryAndRequiredIndexes()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "backend/InovaSkill.Importer.Infrastructure/Persistence/Migrations/202608110003_AddGenericJobEngine.cs"));
        Assert.Contains("ParametersJson", source);
        Assert.Contains("ResultJson", source);
        Assert.Contains("ContractVersion", source);
        Assert.Contains("job_schedules", source);
        Assert.Contains("RetriedFromJobExecutionId", source);
        Assert.Contains("IX_job_executions_ScheduleId", source);
        Assert.Contains("jsonb_build_object('importId'", source);
        Assert.DoesNotContain("USING gin", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
