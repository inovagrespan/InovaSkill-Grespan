using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed record DieselPriceResearchResult(
    string City,
    string State,
    string FuelType,
    decimal AveragePricePerLiter,
    int? StationsSurveyed,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTime ResearchedAt,
    IReadOnlyList<ChatModelSource> Sources);

public sealed class DieselPriceResearchService(
    IChatModelClient modelClient,
    IOptions<AssistantOptions> options)
{
    private const string City = "Marília";
    private const string State = "SP";
    private const string FuelType = "Óleo Diesel S10";
    private static readonly object ResultFormat = new
    {
        type = "json_schema",
        name = "diesel_price_research",
        strict = true,
        schema = new
        {
            type = "object",
            properties = new
            {
                averagePricePerLiter = new { type = "number", minimum = 0 },
                stationsSurveyed = new { type = new[] { "integer", "null" }, minimum = 0 },
                periodStart = new { type = "string", description = "Data inicial no formato YYYY-MM-DD" },
                periodEnd = new { type = "string", description = "Data final no formato YYYY-MM-DD" }
            },
            required = new[] { "averagePricePerLiter", "stationsSurveyed", "periodStart", "periodEnd" },
            additionalProperties = false
        }
    };

    public async Task<DieselPriceResearchResult> ResearchAsync(CancellationToken cancellationToken)
    {
        var response = await modelClient.SendAsync(new ChatModelRequest(
            options.Value.Model,
            "Pesquise exclusivamente fontes oficiais da ANP. Retorne a pesquisa semanal mais recente que contenha o preço médio de revenda do Óleo Diesel S10 no município solicitado. Não estime nem use média estadual. Datas devem usar YYYY-MM-DD.",
            [new ChatModelInputMessage("user", "Preço médio mais recente do Óleo Diesel S10 em Marília, SP, segundo a ANP.")],
            [],
            Purpose: ChatModelRequestPurpose.ExternalResearch,
            TextFormat: ResultFormat,
            EnableWebSearch: true), cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Text))
            throw new InvalidOperationException("A pesquisa não retornou o preço do diesel.");

        using var document = ParseResponse(response.Text);
        var root = document.RootElement;
        var average = root.GetProperty("averagePricePerLiter").GetDecimal();
        var stationsElement = root.GetProperty("stationsSurveyed");
        int? stations = stationsElement.ValueKind == JsonValueKind.Null ? null : stationsElement.GetInt32();
        var periodStart = ReadDate(root, "periodStart");
        var periodEnd = ReadDate(root, "periodEnd");
        if (average <= 0 || periodEnd < periodStart)
            throw new InvalidOperationException("A pesquisa retornou dados de combustível inválidos.");

        var sources = (response.Sources ?? [])
            .Where(source => Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
                (uri.Host.Equals("www.gov.br", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.Equals("gov.br", StringComparison.OrdinalIgnoreCase)) &&
                uri.AbsolutePath.StartsWith("/anp/", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(source => source.Url)
            .ToArray();
        if (sources.Length == 0)
            throw new InvalidOperationException("A pesquisa não apresentou uma fonte oficial da ANP.");

        return new(City, State, FuelType, decimal.Round(average, 3, MidpointRounding.AwayFromZero),
            stations, periodStart, periodEnd, DateTime.UtcNow, sources);
    }

    private static DateOnly ReadDate(JsonElement root, string propertyName) =>
        DateOnly.TryParseExact(root.GetProperty(propertyName).GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value)
            ? value
            : throw new InvalidOperationException("A pesquisa retornou um período inválido.");

    private static JsonDocument ParseResponse(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("A pesquisa retornou dados de combustível inválidos.", exception);
        }
    }
}
