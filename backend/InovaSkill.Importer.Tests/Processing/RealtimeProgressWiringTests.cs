using System.Text;

namespace InovaSkill.Importer.Tests.Processing;

public class RealtimeProgressWiringTests
{
    [Fact]
    public void Worker_UsesRedisProgressNotifier()
    {
        var program = ReadSource("backend", "InovaSkill.Importer.Worker", "Program.cs");

        Assert.Contains("AddScoped<IFileJobProgressNotifier, RedisFileJobProgressNotifier>()", program);
    }

    [Fact]
    public void Api_UsesRedisProgressBridgeForSignalR()
    {
        var program = ReadSource("backend", "InovaSkill.Importer.Api", "Program.cs");

        Assert.Contains("AddScoped<IFileJobProgressNotifier, RedisFileJobProgressNotifier>()", program);
        Assert.Contains("AddHostedService<RedisFileJobProgressBroadcastService>()", program);
    }

    [Fact]
    public void RedisBroadcastService_ForwardsJobUpdatedSignal()
    {
        var serviceSource = ReadSource("backend", "InovaSkill.Importer.Api", "Realtime", "RedisFileJobProgressBroadcastService.cs");

        Assert.Contains("SubscribeAsync(_channel)", serviceSource);
        Assert.Contains("SendAsync(\"jobUpdated\"", serviceSource);
        Assert.Contains("FileJobProgressHub.BuildJobGroup(payload.JobId)", serviceSource);
    }

    private static string ReadSource(params string[] relativePath)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var segments = new[] { repositoryRoot }.Concat(relativePath).ToArray();
        var path = Path.GetFullPath(Path.Combine(segments));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
