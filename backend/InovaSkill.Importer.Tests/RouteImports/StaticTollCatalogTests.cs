using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class StaticTollCatalogTests
{
    [Fact]
    public void Estimate_CountsRepeatedPassagesAndAppliesAutomaticDiscount()
    {
        var catalog = new StaticTollCatalog();
        IReadOnlyList<IReadOnlyList<decimal>> geometry =
        [
            [-49.9095m, -22.0908m],
            [-49.50m, -22.00m],
            [-49.50m, -22.00m],
            [-49.9095m, -22.0908m]
        ];

        var result = catalog.Estimate(geometry, 2);

        var passage = Assert.Single(result.Passages, item => item.Plaza.Code == "ENT-MARILIA");
        Assert.Equal(2, passage.Passages);
        Assert.Equal(25.84m, passage.AutomaticUnitTariff);
        Assert.Equal(51.68m, passage.TotalCost);
        Assert.Null(catalog.Current.EffectiveFrom);
    }

    [Fact]
    public void Estimate_ReturnsZeroForEmptyGeometry()
    {
        var result = new StaticTollCatalog().Estimate([], 3);

        Assert.Empty(result.Passages);
        Assert.Equal(0, result.TotalPassages);
        Assert.Equal(0m, result.TotalCost);
    }
}
