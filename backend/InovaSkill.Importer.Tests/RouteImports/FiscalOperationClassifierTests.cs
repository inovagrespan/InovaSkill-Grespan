using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class FiscalOperationClassifierTests
{
    [Theory]
    [InlineData("VENDA", FiscalMovementCategory.Sale)]
    [InlineData("DEVOLUÇÃO DE VENDA", FiscalMovementCategory.Return)]
    [InlineData("BONIFICAÇÃO", FiscalMovementCategory.Bonus)]
    [InlineData("COMODATO", FiscalMovementCategory.Loan)]
    [InlineData("TROCA", FiscalMovementCategory.Exchange)]
    [InlineData("OUTRA", FiscalMovementCategory.Unknown)]
    public void Classify_CentralizesSourceDescriptions(string description, FiscalMovementCategory expected) =>
        Assert.Equal(expected, FiscalOperationClassifier.Classify("", description));
}
