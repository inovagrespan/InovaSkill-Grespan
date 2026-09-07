using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerAddressCompletenessTests
{
    [Theory]
    [InlineData("Rua A", "10", CustomerAddressCompleteness.Complete)]
    [InlineData("Rua A", null, CustomerAddressCompleteness.WithoutNumber)]
    [InlineData("Rua A", "  ", CustomerAddressCompleteness.WithoutNumber)]
    [InlineData(null, "10", CustomerAddressCompleteness.PostalOnly)]
    [InlineData(" ", null, CustomerAddressCompleteness.PostalOnly)]
    public void FromClassifiesObservableAddressFields(
        string? street, string? number, string expected)
    {
        Assert.Equal(expected, CustomerAddressCompleteness.From(street, number));
    }
}
