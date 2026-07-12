namespace InovaSkill.Importer.Application.RouteImports;

public sealed record CommercialSaleQualityInput(
    decimal InvoiceTotalAmount,
    decimal? CustomerAverageTicket,
    int HistoricalSaleDocumentCount,
    bool IsSale);

public sealed record CommercialSaleQualityResult(
    decimal? CustomerAverageTicket,
    int HistoricalSaleDocumentCount,
    decimal? TicketVariationPercentage,
    string Classification,
    string Reason);

public static class CommercialSaleQualityCalculator
{
    public const int MinimumHistoricalSaleDocuments = 2;
    public const decimal TicketVariationAttentionThresholdPercentage = 15m;

    public const string GreatSaleClassification = "Boa venda";
    public const string RegularSaleClassification = "Venda normal";
    public const string AttentionSaleClassification = "Venda de atenção";
    public const string InsufficientHistoryClassification = "Sem histórico suficiente";
    public const string NotApplicableClassification = "Não aplicável";

    public static CommercialSaleQualityResult Calculate(CommercialSaleQualityInput input)
    {
        if (!input.IsSale)
        {
            return new CommercialSaleQualityResult(
                input.CustomerAverageTicket,
                input.HistoricalSaleDocumentCount,
                null,
                NotApplicableClassification,
                "A qualidade comercial é calculada apenas para operações de venda.");
        }

        if (input.CustomerAverageTicket is null or <= 0 ||
            input.HistoricalSaleDocumentCount < MinimumHistoricalSaleDocuments)
        {
            return new CommercialSaleQualityResult(
                input.CustomerAverageTicket,
                input.HistoricalSaleDocumentCount,
                null,
                InsufficientHistoryClassification,
                "Cliente ainda tem poucas notas de venda anteriores para comparação.");
        }

        var variationPercentage = (input.InvoiceTotalAmount - input.CustomerAverageTicket.Value) /
            input.CustomerAverageTicket.Value * 100m;

        if (variationPercentage >= TicketVariationAttentionThresholdPercentage)
        {
            return new CommercialSaleQualityResult(
                input.CustomerAverageTicket,
                input.HistoricalSaleDocumentCount,
                variationPercentage,
                GreatSaleClassification,
                "Nota acima do ticket médio histórico do cliente.");
        }

        if (variationPercentage <= -TicketVariationAttentionThresholdPercentage)
        {
            return new CommercialSaleQualityResult(
                input.CustomerAverageTicket,
                input.HistoricalSaleDocumentCount,
                variationPercentage,
                AttentionSaleClassification,
                "Nota abaixo do ticket médio histórico do cliente.");
        }

        return new CommercialSaleQualityResult(
            input.CustomerAverageTicket,
            input.HistoricalSaleDocumentCount,
            variationPercentage,
            RegularSaleClassification,
            "Nota dentro da faixa esperada do ticket médio histórico do cliente.");
    }
}
