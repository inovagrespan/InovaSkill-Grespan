using InovaSkill.Importer.Api.Assistant;

namespace InovaSkill.Importer.Tests.Api;

public sealed class ExternalResearchQuerySanitizerTests
{
    [Fact]
    public void Sanitize_RemovesInternalNamesDocumentsCodesAndValues()
    {
        var payloads = new[] { "{\"customer\":\"Mercado Central\",\"code\":\"ABC-99\"}" };

        var result = ExternalResearchQuerySanitizer.Sanitize(
            "Pesquisar Mercado Central CPF 123.456.789-00 código ABC-99 e valor R$ 125,50 sobre armazenamento congelado",
            payloads);

        Assert.NotNull(result);
        Assert.DoesNotContain("Mercado Central", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123", result);
        Assert.DoesNotContain("ABC-99", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("125,50", result);
        Assert.Contains("armazenamento congelado", result);
    }

    [Fact]
    public void Sanitize_RejectsQueryWhenOnlySensitiveContextRemains()
    {
        var result = ExternalResearchQuerySanitizer.Sanitize(
            "Mercado Central 123.456.789-00",
            ["{\"customer\":\"Mercado Central\"}"]);

        Assert.Null(result);
    }
}
