using System.Net;
using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OsrmTableClientTests
{
    [Fact]
    public async Task GetTableAsync_SerializesLongitudeBeforeLatitudeAndPreservesDirectionalValues()
    {
        var handler = new RecordingHandler(_ => """
            {"code":"Ok","durations":[[0,10],[20,0]],"distances":[[0,100],[250,0]]}
            """);
        var client = Client(handler, blockSize: 10);

        var result = await client.GetTableAsync(Request(), CancellationToken.None);

        Assert.Contains("-49.95,-22.217", handler.Paths.Single());
        Assert.Contains("-49.9,-22.2", handler.Paths.Single());
        Assert.Contains("annotations=duration,distance", handler.Paths.Single());
        Assert.Equal(10m, result.DurationsSeconds[0][1]);
        Assert.Equal(20m, result.DurationsSeconds[1][0]);
        Assert.Equal(250m, result.DistancesMeters[1][0]);
        Assert.Equal("OSRM_TABLE_DRIVING", result.Source);
    }

    [Fact]
    public async Task GetTableAsync_SplitsAndReassemblesMatrixBlocks()
    {
        var handler = new RecordingHandler(request =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var sourceCount = query["sources"]!.Split(';').Length;
            var destinationCount = query["destinations"]!.Split(';').Length;
            var rows = Enumerable.Range(0, sourceCount)
                .Select(_ => Enumerable.Repeat("1", destinationCount).ToArray())
                .Select(row => "[" + string.Join(',', row) + "]");
            var matrix = "[" + string.Join(',', rows) + "]";
            return $"{{\"code\":\"Ok\",\"durations\":{matrix},\"distances\":{matrix}}}";
        });
        var points = new[]
        {
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -22, -49),
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -21, -48),
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -20, -47)
        };

        var result = await Client(handler, blockSize: 2).GetTableAsync(new("MONDAY", points), CancellationToken.None);

        Assert.Equal(4, handler.Paths.Count);
        Assert.All(result.DurationsSeconds, row => Assert.Equal(3, row.Count));
        Assert.All(result.DurationsSeconds.SelectMany(row => row), value => Assert.Equal(1m, value));
    }

    [Theory]
    [InlineData("{\"code\":\"Ok\",\"durations\":[[0,null],[1,0]],\"distances\":[[0,1],[1,0]]}")]
    [InlineData("{\"code\":\"Ok\",\"durations\":[[0]],\"distances\":[[0]]}")]
    [InlineData("{\"code\":\"NoTable\"}")]
    public async Task GetTableAsync_RejectsIncompleteOrInvalidResponses(string json)
    {
        var exception = await Assert.ThrowsAsync<OsrmTableException>(() =>
            Client(new RecordingHandler(_ => json), 10).GetTableAsync(Request(), CancellationToken.None));
        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task GetTableAsync_RejectsInvalidPointContractBeforeHttpCall()
    {
        var handler = new RecordingHandler(_ => "{}");
        var points = new[]
        {
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -22, -49),
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -21, -48)
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Client(handler, 10).GetTableAsync(new("MONDAY", points), CancellationToken.None));
        Assert.Empty(handler.Paths);
    }

    private static OsrmTableRequest Request() => new("MONDAY", new[]
    {
        new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -22.217m, -49.950m),
        new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -22.200m, -49.900m)
    });

    private static OsrmTableClient Client(HttpMessageHandler handler, int blockSize) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://osrm.local/") },
        Options.Create(new OsrmOptions { MatrixBlockSize = blockSize, MaximumParallelRequests = 2 }));

    private sealed class RecordingHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
        }
    }
}
