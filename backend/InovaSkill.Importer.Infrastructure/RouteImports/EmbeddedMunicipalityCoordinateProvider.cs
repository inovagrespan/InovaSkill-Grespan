using System.Globalization;
using System.Reflection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record MunicipalityCoordinateLookup(
    string IbgeCode,
    string StateCode,
    string NormalizedName,
    decimal Latitude,
    decimal Longitude);

public interface IMunicipalityCoordinateProvider
{
    string SourceName { get; }
    Task<MunicipalityCoordinateLookup?> ResolveAsync(
        Municipality municipality,
        CancellationToken cancellationToken);
}

public sealed class EmbeddedMunicipalityCoordinateProvider : IMunicipalityCoordinateProvider
{
    public const string Source = "github.com/kelvins/municipios-brasileiros";
    private static readonly Lazy<IReadOnlyList<MunicipalityCoordinateLookup>> LazyCoordinates =
        new(LoadCoordinates);

    private static readonly IReadOnlyDictionary<string, string> StateCodesByIbgeStateCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["11"] = "RO", ["12"] = "AC", ["13"] = "AM", ["14"] = "RR", ["15"] = "PA",
            ["16"] = "AP", ["17"] = "TO", ["21"] = "MA", ["22"] = "PI", ["23"] = "CE",
            ["24"] = "RN", ["25"] = "PB", ["26"] = "PE", ["27"] = "AL", ["28"] = "SE",
            ["29"] = "BA", ["31"] = "MG", ["32"] = "ES", ["33"] = "RJ", ["35"] = "SP",
            ["41"] = "PR", ["42"] = "SC", ["43"] = "RS", ["50"] = "MS", ["51"] = "MT",
            ["52"] = "GO", ["53"] = "DF",
        };

    public string SourceName => Source;

    public Task<MunicipalityCoordinateLookup?> ResolveAsync(
        Municipality municipality,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var coordinates = LazyCoordinates.Value;
        MunicipalityCoordinateLookup? match = null;
        if (!string.IsNullOrWhiteSpace(municipality.IbgeCode))
        {
            match = coordinates.SingleOrDefault(item => item.IbgeCode == municipality.IbgeCode);
            if (match is not null) return Task.FromResult<MunicipalityCoordinateLookup?>(match);
        }

        match = coordinates.SingleOrDefault(item =>
            item.StateCode == municipality.StateCode &&
            item.NormalizedName == municipality.NormalizedName);
        return Task.FromResult<MunicipalityCoordinateLookup?>(match);
    }

    private static IReadOnlyList<MunicipalityCoordinateLookup> LoadCoordinates()
    {
        var assembly = typeof(EmbeddedMunicipalityCoordinateProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("municipios.csv", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Base de coordenadas municipais não foi encontrada.");
        using var reader = new StreamReader(stream);
        _ = reader.ReadLine();
        var rows = new List<MunicipalityCoordinateLookup>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var columns = line.Split(',');
            if (columns.Length < 6) continue;
            if (!StateCodesByIbgeStateCode.TryGetValue(columns[5].Trim(), out var stateCode))
                continue;
            rows.Add(new MunicipalityCoordinateLookup(
                columns[0].Trim(),
                stateCode,
                MunicipalityNameNormalizer.Normalize(columns[1]),
                decimal.Parse(columns[2], CultureInfo.InvariantCulture),
                decimal.Parse(columns[3], CultureInfo.InvariantCulture)));
        }
        return rows;
    }
}
