using InovaSkill.Importer.Api.Assistant;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class DieselPriceResearchServiceTests
{
    [Fact]
    public async Task ResearchAsync_ReturnsRoundedOfficialAnpResult()
    {
        var model = new StubModelClient(new ChatModelResponse(
            "response-1",
            """{"averagePricePerLiter":6.1236,"stationsSurveyed":8,"periodStart":"2026-08-23","periodEnd":"2026-08-29"}""",
            [],
            [new ChatModelSource("ANP — preços semanais", "https://www.gov.br/anp/pt-br/assuntos/precos")]));
        var service = CreateService(model);

        var result = await service.ResearchAsync(CancellationToken.None);

        Assert.Equal("Marília", result.City);
        Assert.Equal("Óleo Diesel S10", result.FuelType);
        Assert.Equal(6.124m, result.AveragePricePerLiter);
        Assert.Equal(8, result.StationsSurveyed);
        Assert.Equal(new DateOnly(2026, 8, 23), result.PeriodStart);
        Assert.Single(result.Sources);
        Assert.True(model.LastRequest!.EnableWebSearch);
        Assert.NotNull(model.LastRequest.TextFormat);
    }

    [Fact]
    public async Task ResearchAsync_RejectsResultWithoutOfficialAnpSource()
    {
        var model = new StubModelClient(new ChatModelResponse(
            "response-1",
            """{"averagePricePerLiter":6.1,"stationsSurveyed":null,"periodStart":"2026-08-23","periodEnd":"2026-08-29"}""",
            [],
            [new ChatModelSource("Blog", "https://example.com/preco")]));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(model).ResearchAsync(CancellationToken.None));

        Assert.Contains("fonte oficial da ANP", error.Message);
    }

    [Fact]
    public async Task ResearchAsync_RejectsMalformedResponse()
    {
        var model = new StubModelClient(new ChatModelResponse("response-1", "não é json", [], []));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(model).ResearchAsync(CancellationToken.None));

        Assert.Contains("dados de combustível inválidos", error.Message);
    }

    [Theory]
    [InlineData("0", "2026-08-23", "2026-08-29")]
    [InlineData("6.1", "2026-08-30", "2026-08-29")]
    public async Task ResearchAsync_RejectsInvalidMetrics(string average, string start, string end)
    {
        var text = $$"""{"averagePricePerLiter":{{average}},"stationsSurveyed":1,"periodStart":"{{start}}","periodEnd":"{{end}}"}""";
        var model = new StubModelClient(new ChatModelResponse("response-1", text, [],
            [new ChatModelSource("ANP", "https://www.gov.br/anp/pt-br/precos")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(model).ResearchAsync(CancellationToken.None));
    }

    private static DieselPriceResearchService CreateService(IChatModelClient model) =>
        new(model, Options.Create(new AssistantOptions { Model = "test-model" }));

    private sealed class StubModelClient(ChatModelResponse response) : IChatModelClient
    {
        public ChatModelRequest? LastRequest { get; private set; }

        public Task<ChatModelResponse> SendAsync(ChatModelRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
