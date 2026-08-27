using InovaSkill.Importer.Infrastructure.WhatsApp;

namespace InovaSkill.Importer.Tests.Api;

public sealed class WhatsAppAnswerFormatterTests
{
    [Fact]
    public void Format_OrganizesCriticalRoutesAsNumberedVisualBlocks()
    {
        const string answer = """
            Foram encontradas duas rotas críticas.
            [ROTA] Rota Marília | Ocupação: 97,4% | Status: Crítico | Motivo: ocupação acima do limite saudável de 95%.
            [ROTA] Rota Bauru | Ocupação: 99,1% | Status: Crítico | Motivo: capacidade excedida.
            Período dos dados: 18/08/2026.
            """;

        var formatted = WhatsAppAnswerFormatter.Format(answer);

        Assert.DoesNotContain("[ROTA]", formatted);
        Assert.Contains("🚨 *1. Rota Marília*", formatted);
        Assert.Contains("• Ocupação: 97,4%", formatted);
        Assert.Contains("• Status: Crítico", formatted);
        Assert.Contains("🚨 *2. Rota Bauru*", formatted);
        Assert.Contains("📅 *Período dos dados:* 18/08/2026.", formatted);
    }

    [Fact]
    public void Format_PreservesPlainAnswersWithoutRouteContracts()
    {
        const string answer = "Não foram encontradas rotas críticas.";

        var formatted = WhatsAppAnswerFormatter.Format(answer);

        Assert.Equal(answer, formatted);
    }

    [Fact]
    public void Format_HighlightsDataPeriodForQuickIdentification()
    {
        const string answer = "Resumo operacional.\nPeríodo dos dados: 01/08/2026 a 18/08/2026.";

        var formatted = WhatsAppAnswerFormatter.Format(answer);

        Assert.Equal("Resumo operacional.\n📅 *Período dos dados:* 01/08/2026 a 18/08/2026.", formatted);
    }

    [Fact]
    public void Format_HandlesIncompleteRouteContractWithoutLosingInformation()
    {
        const string answer = "[ROTA] Rota sem ocupação | dado complementar";

        var formatted = WhatsAppAnswerFormatter.Format(answer);

        Assert.Equal("🚨 *1. Rota sem ocupação*\n• dado complementar", formatted);
    }
}
