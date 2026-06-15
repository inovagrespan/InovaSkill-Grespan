using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Tests.Processing;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddImportInfrastructure_RegistersDefaultFileJobProgressNotifier()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();

        services.AddImportInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<IFileJobProgressNotifier>();

        Assert.IsType<NullFileJobProgressNotifier>(notifier);
    }
}
