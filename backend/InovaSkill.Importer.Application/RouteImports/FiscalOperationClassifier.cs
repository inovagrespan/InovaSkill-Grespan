using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Application.RouteImports;

public static class FiscalOperationClassifier
{
    public static FiscalMovementCategory Classify(string? code, string? description)
    {
        var value = MunicipalityNameNormalizer.Normalize($"{code} {description}");
        if (value.Contains("DEVOLUCAO", StringComparison.Ordinal)) return FiscalMovementCategory.Return;
        if (value.Contains("BONIFICACAO", StringComparison.Ordinal)) return FiscalMovementCategory.Bonus;
        if (value.Contains("COMODATO", StringComparison.Ordinal)) return FiscalMovementCategory.Loan;
        if (value.Contains("TROCA", StringComparison.Ordinal)) return FiscalMovementCategory.Exchange;
        if (value.Contains("VENDA", StringComparison.Ordinal)) return FiscalMovementCategory.Sale;
        return FiscalMovementCategory.Unknown;
    }
}
