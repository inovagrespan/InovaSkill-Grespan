using System.Globalization;
using System.Net.Http.Json;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmTableClient(HttpClient httpClient, IOptions<OsrmOptions> options) : IOsrmTableClient
{
    private const string SourceName = "OSRM_TABLE_DRIVING";
    private readonly OsrmOptions settings = ValidateOptions(options.Value);

    public async Task<OsrmTableResult> GetTableAsync(
        OsrmTableRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var count = request.Points.Count;
        var durations = CreateMatrix(count);
        var distances = CreateMatrix(count);
        var blocks = Enumerable.Range(0, count)
            .Chunk(settings.MatrixBlockSize)
            .Select(chunk => chunk.ToArray())
            .ToArray();
        using var gate = new SemaphoreSlim(settings.MaximumParallelRequests);

        var tasks = (from sources in blocks
                     from destinations in blocks
                     select FillBlockAsync(request.Points, sources, destinations, durations, distances, gate, cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);

        return new OsrmTableResult(
            SourceName,
            request.Points,
            durations.Select(row => (IReadOnlyList<decimal>)row).ToArray(),
            distances.Select(row => (IReadOnlyList<decimal>)row).ToArray());
    }

    public async Task<bool> IsHealthyAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken)
    {
        ValidateCoordinate(latitude, longitude, "depósito");
        var coordinate = Coordinate(longitude, latitude);
        try
        {
            using var response = await httpClient.GetAsync(
                $"nearest/v1/driving/{coordinate}?number=1",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private async Task FillBlockAsync(
        IReadOnlyList<OsrmMatrixPoint> allPoints,
        int[] sourceIndexes,
        int[] destinationIndexes,
        decimal[][] durations,
        decimal[][] distances,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var globalIndexes = sourceIndexes.Concat(destinationIndexes).Distinct().ToArray();
            var localIndexByGlobal = globalIndexes.Select((global, local) => (global, local))
                .ToDictionary(item => item.global, item => item.local);
            var coordinates = string.Join(';', globalIndexes.Select(index =>
                Coordinate(allPoints[index].Longitude, allPoints[index].Latitude)));
            var sources = string.Join(';', sourceIndexes.Select(index => localIndexByGlobal[index]));
            var destinations = string.Join(';', destinationIndexes.Select(index => localIndexByGlobal[index]));
            var path = $"table/v1/driving/{coordinates}?annotations=duration,distance&sources={sources}&destinations={destinations}";

            OsrmTablePayload payload;
            try
            {
                payload = await httpClient.GetFromJsonAsync<OsrmTablePayload>(path, cancellationToken)
                    ?? throw new OsrmTableException("O OSRM retornou uma resposta vazia.");
            }
            catch (OsrmTableException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                throw new OsrmTableException("Não foi possível obter a matriz rodoviária do OSRM.", exception);
            }

            if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase))
                throw new OsrmTableException($"O OSRM rejeitou a matriz: {payload.Code ?? "código ausente"}.");
            ValidatePayload(payload, sourceIndexes.Length, destinationIndexes.Length);

            for (var source = 0; source < sourceIndexes.Length; source++)
            for (var destination = 0; destination < destinationIndexes.Length; destination++)
            {
                durations[sourceIndexes[source]][destinationIndexes[destination]] =
                    DecimalValue(payload.Durations![source][destination], "duração");
                distances[sourceIndexes[source]][destinationIndexes[destination]] =
                    DecimalValue(payload.Distances![source][destination], "distância");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static decimal[][] CreateMatrix(int size) =>
        Enumerable.Range(0, size).Select(_ => new decimal[size]).ToArray();

    private static void ValidateRequest(OsrmTableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Weekday))
            throw new ArgumentException("O dia da semana da matriz é obrigatório.", nameof(request));
        if (request.Points.Count < 2)
            throw new ArgumentException("A matriz exige o depósito e pelo menos uma cidade.", nameof(request));
        if (request.Points[0].Type != OsrmMatrixPointTypes.Depot ||
            request.Points.Skip(1).Any(point => point.Type != OsrmMatrixPointTypes.Municipality))
            throw new ArgumentException("O primeiro ponto deve ser o depósito e os demais devem ser municípios.", nameof(request));
        if (request.Points.Select(point => point.Id).Distinct().Count() != request.Points.Count)
            throw new ArgumentException("A matriz não aceita pontos duplicados.", nameof(request));
        foreach (var point in request.Points)
            ValidateCoordinate(point.Latitude, point.Longitude, point.Type);
    }

    private static void ValidateCoordinate(decimal latitude, decimal longitude, string label)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentException($"A coordenada de {label} é inválida.");
    }

    private static void ValidatePayload(OsrmTablePayload payload, int sources, int destinations)
    {
        if (payload.Durations?.Length != sources || payload.Distances?.Length != sources ||
            payload.Durations.Any(row => row.Length != destinations) ||
            payload.Distances.Any(row => row.Length != destinations))
            throw new OsrmTableException("O OSRM retornou uma matriz com dimensões inválidas.");
    }

    private static decimal DecimalValue(decimal? value, string field) =>
        value is null || value < 0
            ? throw new OsrmTableException($"O OSRM retornou {field} nula ou inválida entre pontos obrigatórios.")
            : value.Value;

    private static string Coordinate(decimal longitude, decimal latitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{longitude:0.######},{latitude:0.######}");

    private static OsrmOptions ValidateOptions(OsrmOptions value)
    {
        if (value.MatrixBlockSize <= 0 || value.MaximumParallelRequests <= 0)
            throw new InvalidOperationException("Osrm:MatrixBlockSize e Osrm:MaximumParallelRequests devem ser positivos.");
        return value;
    }

    private sealed record OsrmTablePayload(
        string? Code,
        decimal?[][]? Durations,
        decimal?[][]? Distances);
}
