using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DailyOptimizationStopConsolidatorTests
{
    [Fact]
    public void Consolidate_SumsLoadAndDeliveriesForTheSameMunicipality()
    {
        var municipalityId = Guid.NewGuid();
        var firstRouteId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondRouteId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var result = DailyOptimizationStopConsolidator.Consolidate([
            new(municipalityId, "Cidade A", 120.5m, 2, secondRouteId, 3),
            new(municipalityId, "Cidade A", 79.5m, 4, firstRouteId, 7)
        ]);

        var stop = Assert.Single(result);
        Assert.Equal(200m, stop.LoadKg);
        Assert.Equal(6, stop.Deliveries);
        Assert.Equal(firstRouteId, stop.OriginalRouteId);
        Assert.Equal(7, stop.OriginalSequence);
    }

    [Fact]
    public void Consolidate_KeepsDifferentMunicipalitiesSeparate()
    {
        var result = DailyOptimizationStopConsolidator.Consolidate([
            new(Guid.NewGuid(), "Cidade A", 10m, 1, Guid.NewGuid(), 1),
            new(Guid.NewGuid(), "Cidade B", 20m, 2, Guid.NewGuid(), 2)
        ]);

        Assert.Equal(2, result.Count);
        Assert.Equal(30m, result.Sum(stop => stop.LoadKg));
        Assert.Equal(3, result.Sum(stop => stop.Deliveries));
    }
}
