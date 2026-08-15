using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class QueryIndexModelTests
{
    [Theory]
    [InlineData(typeof(Route), "ImportId", "OverallOccupancy")]
    [InlineData(typeof(Customer), "DataSourceId", "ExternalCode", "BranchCode")]
    [InlineData(typeof(CustomerSnapshot), "ImportId", "MunicipalityId")]
    [InlineData(typeof(CustomerSnapshot), "ImportId", "CustomerType")]
    [InlineData(typeof(RouteCustomerAssignment), "RouteId", "CustomerId")]
    [InlineData(typeof(RouteCustomerAssignment), "RouteId", "Source")]
    [InlineData(typeof(Product), "ErpCode")]
    [InlineData(typeof(Product), "OperationalCode")]
    [InlineData(typeof(InventorySnapshot), "ImportId", "AvailableQuantity")]
    [InlineData(typeof(DailyInventoryRecord), "ProductId", "Date")]
    [InlineData(typeof(AiProviderCall), "CreatedAt")]
    public void Model_HasIndexesForCurrentFilterAndOrderingPatterns(
        Type entityType,
        params string[] expectedProperties)
    {
        using var dbContext = new ImportDbContext(
            new DbContextOptionsBuilder<ImportDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var entity = dbContext.Model.FindEntityType(entityType);
        var hasIndex = entity!.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(expectedProperties));

        Assert.True(
            hasIndex,
            $"Índice esperado em {entityType.Name} ({string.Join(", ", expectedProperties)}).");
    }
}
