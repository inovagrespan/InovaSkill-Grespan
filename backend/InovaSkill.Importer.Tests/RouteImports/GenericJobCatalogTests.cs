using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GenericJobCatalogTests
{
    [Theory]
    [InlineData(OperationalJobCodes.ProcessImport, BackgroundJobQueues.Imports, false)]
    [InlineData(OperationalJobCodes.MunicipalityCoordinateEnrichment, BackgroundJobQueues.Default, true)]
    [InlineData(OperationalJobCodes.CustomerRegistrationAddressEnrichment, BackgroundJobQueues.Default, true)]
    [InlineData(OperationalJobCodes.WhatsAppMessageProcessing, BackgroundJobQueues.Default, false)]
    public void Catalog_ResolvesEveryJobByCaseInsensitiveKeyWithValidVersionedJson(
        string jobType, string expectedQueue, bool manualRunAllowed)
    {
        Assert.True(OperationalJobCatalog.TryGet(jobType.ToLowerInvariant(), out var definition));
        Assert.Same(definition, OperationalJobCatalog.GetRequired(jobType));
        Assert.Equal(expectedQueue, definition.Queue);
        Assert.Equal(manualRunAllowed, definition.ManualRunAllowed);
        Assert.Equal(1, definition.ContractVersion);
        using var document = JsonDocument.Parse(definition.ExampleParametersJson);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void Catalog_HasUniqueDictionaryKeyForEveryDefinition()
    {
        Assert.Equal(4, OperationalJobCatalog.All.Count);
        Assert.Equal(4, OperationalJobCatalog.All.Select(item => item.JobType).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
