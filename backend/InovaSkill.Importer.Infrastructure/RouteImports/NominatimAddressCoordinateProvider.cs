using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record AddressCoordinateQuery(string? StreetType, string Street, string? Number, string? Neighborhood,
    string City, string StateCode, string? PostalCode);
public sealed record AddressCoordinateLookup(string Status, decimal? Latitude, decimal? Longitude,
    string? PlaceId, string? DisplayName, string? FailureReason, string? MatchLevel = null, string? Source = null);

public static class AddressCoordinateMatchLevels
{
    public const string Exact = "EXACT";
    public const string Street = "STREET";
    public const string PostalCode = "POSTAL_CODE";
    public const string Municipality = "MUNICIPALITY";
}

public static class AddressCoordinateFailureReasons
{
    public const string InsufficientData = "DADOS_INSUFICIENTES:";
    public const string InvalidOrIncompatiblePostalCode = "CEP_INVALIDO_OU_INCOMPATIVEL:";
    public const string IncompatibleMunicipalityOrState = "MUNICIPIO_OU_UF_INCOMPATIVEL:";
    public const string ExhaustedFallbacks = "FALLBACKS_ESGOTADOS:";
}

public interface ICustomerAddressCoordinateProvider
{
    string SourceName { get; }
    Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken);
}

public interface INominatimRequestGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class NominatimRequestGate(IOptions<NominatimOptions> options) : INominatimRequestGate
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private DateTime lastRequestStartedAt = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var interval = TimeSpan.FromMilliseconds(Math.Max(1000, options.Value.MinimumRequestIntervalMilliseconds));
            var remaining = interval - (DateTime.UtcNow - lastRequestStartedAt);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
            lastRequestStartedAt = DateTime.UtcNow;
        }
        finally { mutex.Release(); }
    }
}

public sealed class NominatimAddressCoordinateProvider(
    HttpClient httpClient,
    INominatimRequestGate requestGate,
    IOptions<BrasilApiOptions> brasilApiOptions,
    IOptions<NominatimOptions> nominatimOptions) : ICustomerAddressCoordinateProvider
{
    public string SourceName => "NOMINATIM";

    public async Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.City) || string.IsNullOrWhiteSpace(query.StateCode))
            return NotFound(AddressCoordinateFailureReasons.InsufficientData,
                "Município e UF são obrigatórios para validar a coordenada.");

        var unsuccessfulReasons = new List<string>();
        var street = FormatStreet(query.StreetType, query.Street);
        var postalCode = FormatPostalCode(query.PostalCode);
        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(query.Number))
        {
            var exact = await SearchAsync([street, query.Number, query.Neighborhood, query.City,
                query.StateCode, postalCode, "Brasil"], query, requireHouseNumber: true,
                AddressCoordinateMatchLevels.Exact, cancellationToken);
            if (exact.Status == "RESOLVED" || exact.Status == "FAILED") return exact;
            AddReason(exact);
        }
        if (string.IsNullOrWhiteSpace(query.Number) && !string.IsNullOrWhiteSpace(postalCode))
        {
            var postalCodeApproximation = await SearchPostalCodeAsync(postalCode, query, cancellationToken);
            if (postalCodeApproximation.Status == "RESOLVED" || postalCodeApproximation.Status == "FAILED")
                return postalCodeApproximation;
            AddReason(postalCodeApproximation);
        }
        if (!string.IsNullOrWhiteSpace(street))
        {
            var streetApproximation = await SearchAsync([street, query.Neighborhood, query.City,
                query.StateCode, postalCode, "Brasil"], query, requireHouseNumber: false,
                AddressCoordinateMatchLevels.Street, cancellationToken);
            if (streetApproximation.Status == "RESOLVED" || streetApproximation.Status == "FAILED")
                return streetApproximation;
            AddReason(streetApproximation);
        }
        if (!string.IsNullOrWhiteSpace(query.Number) && !string.IsNullOrWhiteSpace(postalCode))
        {
            var postalCodeApproximation = await SearchPostalCodeAsync(postalCode, query, cancellationToken);
            if (postalCodeApproximation.Status == "RESOLVED" || postalCodeApproximation.Status == "FAILED")
                return postalCodeApproximation;
            AddReason(postalCodeApproximation);
        }
        var municipalityApproximation = await SearchAsync([query.City, query.StateCode, "Brasil"], query,
            requireHouseNumber: false, AddressCoordinateMatchLevels.Municipality, cancellationToken);
        if (municipalityApproximation.Status != "NOT_FOUND") return municipalityApproximation;
        AddReason(municipalityApproximation);

        var category = unsuccessfulReasons.Any(reason => reason.StartsWith(
            AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState, StringComparison.Ordinal))
                ? AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState
                : unsuccessfulReasons.Any(reason => reason.StartsWith(
                    AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode, StringComparison.Ordinal))
                    ? AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode
                    : string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(postalCode)
                        ? AddressCoordinateFailureReasons.InsufficientData
                        : AddressCoordinateFailureReasons.ExhaustedFallbacks;
        return NotFound(category, "Endereço, CEP e município não foram resolvidos pelos fallbacks disponíveis.");

        void AddReason(AddressCoordinateLookup lookup)
        {
            if (!string.IsNullOrWhiteSpace(lookup.FailureReason)) unsuccessfulReasons.Add(lookup.FailureReason);
        }
    }

    private async Task<AddressCoordinateLookup> SearchPostalCodeAsync(
        string postalCode,
        AddressCoordinateQuery query,
        CancellationToken cancellationToken)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        if (digits.Length != 8)
            return NotFound(AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode,
                "O CEP deve conter oito dígitos.");
        var baseUrl = brasilApiOptions.Value.BaseUrl.TrimEnd('/') + "/";
        using var response = await SendWithTransportRetryAsync(
            new Uri(new Uri(baseUrl), $"cep/v2/{digits}"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return NotFound(AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode, "CEP não encontrado.");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BrasilApiPostalCodeResult>(cancellationToken);
        if (result is null || !SameText(result.City, query.City) ||
            !string.Equals(result.State, query.StateCode, StringComparison.OrdinalIgnoreCase))
            return NotFound(AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode,
                "CEP incompatível com o município ou a UF consultados.");
        if (!decimal.TryParse(result.Location?.Coordinates?.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !decimal.TryParse(result.Location?.Coordinates?.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            return NotFound(AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode,
                "O CEP não possui coordenadas disponíveis.");
        var displayName = string.Join(", ", new[] { result.Street, result.Neighborhood, result.City, result.State, result.Cep }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new("RESOLVED", latitude, longitude, null, displayName, "Coordenada aproximada pelo CEP.",
            AddressCoordinateMatchLevels.PostalCode, "BRASIL_API_POSTAL_CODE");
    }

    private async Task<AddressCoordinateLookup> SearchAsync(
        IEnumerable<string?> parts,
        AddressCoordinateQuery query,
        bool requireHouseNumber,
        string matchLevel,
        CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        var address = string.Join(", ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        var url = "search?q=" + Uri.EscapeDataString(address) +
            "&format=json&addressdetails=1&countrycodes=br&limit=5";
        using var response = await SendWithTransportRetryAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date is { } retryAt
                    ? retryAt - DateTimeOffset.UtcNow
                    : null);
            if (retryAfter > TimeSpan.Zero) await Task.Delay(retryAfter.Value, cancellationToken);
            throw new NominatimRateLimitException(retryAfter);
        }
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<NominatimResult[]>(cancellationToken) ?? [];
        var compatibleResults = results.Where(result => IsCompatibleMunicipality(result, query)).ToArray();
        var result = requireHouseNumber
            ? compatibleResults.FirstOrDefault(item => SameText(item.Address?.HouseNumber, query.Number ?? string.Empty))
            : compatibleResults.FirstOrDefault();
        if (result is null)
            return results.Length > 0 && compatibleResults.Length == 0
                ? NotFound(AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState,
                    "Os resultados encontrados não correspondem ao município e à UF consultados.")
                : NotFound(AddressCoordinateFailureReasons.ExhaustedFallbacks,
                    requireHouseNumber && compatibleResults.Length > 0
                        ? "O município e a UF conferem, mas o número do imóvel não foi confirmado."
                        : "Nenhum resultado encontrado.");
        if (!decimal.TryParse(result.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !decimal.TryParse(result.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            return new("FAILED", null, null, result.PlaceId?.ToString(CultureInfo.InvariantCulture), result.DisplayName, "Coordenadas inválidas no retorno.");
        return new("RESOLVED", latitude, longitude, result.PlaceId?.ToString(CultureInfo.InvariantCulture),
            result.DisplayName, ApproximationReason(matchLevel), matchLevel);
    }

    private static bool IsCompatibleMunicipality(NominatimResult result, AddressCoordinateQuery query)
    {
        var returnedCity = result.Address?.City ?? result.Address?.Town ?? result.Address?.Municipality ?? result.Address?.Village;
        var returnedStateCode = result.Address?.StateCode?.Split('-').LastOrDefault();
        return SameText(returnedCity, query.City) &&
               string.Equals(returnedStateCode, query.StateCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ApproximationReason(string matchLevel) => matchLevel switch
    {
        AddressCoordinateMatchLevels.Street => "Coordenada aproximada pelo logradouro; número não confirmado.",
        AddressCoordinateMatchLevels.PostalCode => "Coordenada aproximada pelo CEP.",
        AddressCoordinateMatchLevels.Municipality => "Coordenada aproximada pelo município.",
        _ => null
    };

    private static AddressCoordinateLookup NotFound(string category, string detail) =>
        new("NOT_FOUND", null, null, null, null, $"{category} {detail}");

    private async Task<HttpResponseMessage> SendWithTransportRetryAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var maximumRetries = Math.Max(0, nominatimOptions.Value.TransportMaximumRetries);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(0,
            nominatimOptions.Value.TransportRetryDelayMilliseconds));
        for (var retry = 0; ; retry++)
        {
            try
            {
                return await httpClient.GetAsync(requestUri, cancellationToken);
            }
            catch (HttpRequestException) when (retry < maximumRetries)
            {
                if (retryDelay > TimeSpan.Zero)
                    await Task.Delay(retryDelay * (retry + 1), cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                var rootCause = exception.GetBaseException().Message;
                throw new HttpRequestException(
                    $"Falha de transporte após {maximumRetries + 1} tentativas: {rootCause}",
                    exception,
                    exception.StatusCode);
            }
        }
    }

    private static bool SameText(string? left, string right) => Normalize(left) == Normalize(right);
    public static string FormatStreet(string? streetType, string street)
    {
        var cleanStreet = street.Trim();
        var cleanType = streetType?.Trim();
        if (string.IsNullOrWhiteSpace(cleanType) || Normalize(cleanStreet).StartsWith(Normalize(cleanType) + " "))
            return cleanStreet;
        return $"{cleanType} {cleanStreet}";
    }

    public static string? FormatPostalCode(string? postalCode)
    {
        var digits = new string((postalCode ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? $"{digits[..5]}-{digits[5..]}" :
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
    }
    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }

    private sealed record NominatimResult(
        [property: JsonPropertyName("place_id")] long? PlaceId,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("address")] NominatimAddress? Address);
    private sealed record NominatimAddress(
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("house_number")] string? HouseNumber,
        [property: JsonPropertyName("ISO3166-2-lvl4")] string? StateCode);
    private sealed record BrasilApiPostalCodeResult(
        [property: JsonPropertyName("cep")] string? Cep,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("neighborhood")] string? Neighborhood,
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("location")] BrasilApiLocation? Location);
    private sealed record BrasilApiLocation(
        [property: JsonPropertyName("coordinates")] BrasilApiCoordinates? Coordinates);
    private sealed record BrasilApiCoordinates(
        [property: JsonPropertyName("longitude")] string? Longitude,
        [property: JsonPropertyName("latitude")] string? Latitude);
}

public sealed class NominatimRateLimitException(TimeSpan? retryAfter)
    : Exception("O Nominatim limitou temporariamente as requisições.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
