namespace InovaSkill.Importer.Tests.Persistence;

public sealed class NotificationSchemaBootstrapTests
{
    [Fact]
    public void Bootstrap_CreatesNotificationsTableAndIndexes()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "InovaSkill.Importer.Infrastructure",
            "Persistence",
            "Bootstrap",
            "DbSchemaBootstrapper.cs"));

        Assert.Contains("EnsureNotificationsTableAsync(context, cancellationToken)", source);
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"Notifications\"", source);
        Assert.Contains("\"IX_Notifications_UserId_Status_CreatedAt\"", source);
        Assert.Contains("\"IX_Notifications_UserId_CreatedAt\"", source);
    }
}
