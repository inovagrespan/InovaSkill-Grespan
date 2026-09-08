using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OpenRouteServiceTableClient(
    HttpClient httpClient,
    IOptions<OpenRouteServiceOptions> options) : IOsrmTableClient
{
    private const string SourceName = "OPENROUTESERVICE_MATRIX_DRIVING_CAR";
    private const int MaximumProviderErrorLength = 500;
    private readonly OpenRouteServiceOptions settings = options.Value;

    public async Task<OsrmTableResult> GetTableAsync(OsrmTableRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new OsrmTableException("A chave da OpenRouteService não foi configurada.");
        if (request.Points.Count < 2)
            throw new OsrmTableException("A matriz exige pelo menos dois pontos.");
        var count = request.Points.Count;
        var durations = CreateMatrix(count);
        var distances = CreateMatrix(count);
        var blockSize = Math.Max(1, settings.MaximumWaypoints / 2);
        var blocks = Enumerable.Range(0, count).Chunk(blockSize).Select(block => block.ToArray()).ToArray();
        foreach (var sources in blocks)
        foreach (var destinations in blocks)
            await FillBlockAsync(request.Points, sources, destinations, durations, distances, cancellationToken);
        return new(SourceName, request.Points,
            durations.Select(row => (IReadOnlyList<decimal>)row).ToArray(),
            distances.Select(row => (IReadOnlyList<decimal>)row).ToArray());
    }

    private async Task FillBlockAsync(
        IReadOnlyList<OsrmMatrixPoint> points,
        int[] sourceIndexes,
        int[] destinationIndexes,
        decimal[][] durations,
        decimal[][] distances,
        CancellationToken cancellationToken)
    {
        var globalIndexes = sourceIndexes.Concat(destinationIndexes).Distinct().ToArray();
        var localIndexByGlobal = globalIndexes.Select((global, local) => (global, local))
            .ToDictionary(item => item.global, item => item.local);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v2/matrix/driving-car");
        message.Headers.Authorization = new("Bearer", settings.ApiKey);
        message.Content = JsonContent.Create(new MatrixRequest(
            globalIndexes.Select(index => new[] { points[index].Longitude, points[index].Latitude }).ToArray(),
            ["duration", "distance"],
            sourceIndexes.Select(index => localIndexByGlobal[index]).ToArray(),
            destinationIndexes.Select(index => localIndexByGlobal[index]).ToArray()));
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new OsrmTableException(
                    $"OpenRouteService recusou a matriz com HTTP {(int)response.StatusCode}: {ReadProviderError(responseBody)}");
            var payload = JsonSerializer.Deserialize<MatrixResponse>(responseBody);
            if (payload?.Durations is null || payload.Distances is null)
                throw new OsrmTableException("A OpenRouteService retornou uma matriz rodoviária incompleta.");
            Validate(payload.Durations, sourceIndexes.Length, destinationIndexes.Length, "duração");
            Validate(payload.Distances, sourceIndexes.Length, destinationIndexes.Length, "distância");
            for (var source = 0; source < sourceIndexes.Length; source++)
            for (var destination = 0; destination < destinationIndexes.Length; destination++)
            {
                durations[sourceIndexes[source]][destinationIndexes[destination]] = Value(payload.Durations[source][destination]);
                distances[sourceIndexes[source]][destinationIndexes[destination]] = Value(payload.Distances[source][destination]);
            }
        }
        catch (OsrmTableException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new OsrmTableException("Não foi possível obter a matriz rodoviária da OpenRouteService.", exception);
        }
    }

    public async Task<bool> IsHealthyAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        try
        {
            var points = new[]
            {
                new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, latitude, longitude),
                new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, latitude + 0.001m, longitude + 0.001m)
            };
            _ = await GetTableAsync(new OsrmTableRequest("HEALTH", points), cancellationToken);
            return true;
        }
        catch (OsrmTableException) { return false; }
    }

    private static decimal Value(decimal? value) => value is >= 0 ? value.Value :
        throw new OsrmTableException("A matriz rodoviária contém trecho sem rota.");
    private static decimal[][] CreateMatrix(int size) =>
        Enumerable.Range(0, size).Select(_ => new decimal[size]).ToArray();

    private static void Validate(decimal?[][] matrix, int rows, int columns, string label)
    {
        if (matrix.Length != rows || matrix.Any(row => row.Length != columns))
            throw new OsrmTableException($"A matriz de {label} possui dimensões inválidas.");
    }

    private static string ReadProviderError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var message = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var nestedMessage)
                    ? nestedMessage.GetString()
                    : root.TryGetProperty("message", out var directMessage) ? directMessage.GetString() : null;
            return string.IsNullOrWhiteSpace(message)
                ? "motivo não informado pelo provedor"
                : message[..Math.Min(message.Length, MaximumProviderErrorLength)];
        }
        catch (JsonException)
        {
            return "motivo não informado pelo provedor";
        }
    }

    private sealed record MatrixRequest(
        [property: JsonPropertyName("locations")] decimal[][] Locations,
        [property: JsonPropertyName("metrics")] string[] Metrics,
        [property: JsonPropertyName("sources")] int[] Sources,
        [property: JsonPropertyName("destinations")] int[] Destinations);
    private sealed record MatrixResponse(
        [property: JsonPropertyName("durations")] decimal?[][]? Durations,
        [property: JsonPropertyName("distances")] decimal?[][]? Distances);
}
