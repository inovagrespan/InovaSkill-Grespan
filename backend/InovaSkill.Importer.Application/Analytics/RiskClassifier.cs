namespace InovaSkill.Importer.Application.Analytics;

public static class RiskClassifier
{
    private const decimal NoRiskMaximumScore = 1.5m;
    private const decimal AttentionMaximumScore = 2m;
    private const decimal AtRiskMaximumScore = 3m;

    public const string InsufficientHistory = "Sem histórico suficiente";
    public const string NoRisk = "Sem risco";
    public const string Attention = "Atenção";
    public const string AtRisk = "Em risco";
    public const string Critical = "Crítico";

    public static string Classify(decimal? riskScore)
    {
        if (riskScore is null)
        {
            return InsufficientHistory;
        }

        if (riskScore <= NoRiskMaximumScore) return NoRisk;
        if (riskScore <= AttentionMaximumScore) return Attention;
        if (riskScore <= AtRiskMaximumScore) return AtRisk;
        return Critical;
    }
}
