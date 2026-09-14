using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class GeoapifyAddressCoordinateProvider(
    HttpClient httpClient,
    IOptions<BrasilApiOptions> brasilApiOptions,
    IOptions<GeoapifyOptions> geoapifyOptions) : ICustomerAddressCoordinateProvider
{
    public string SourceName => "GEOAPIFY";

    public async Task<AddressCoordinateLookup> FindAsync(
        AddressCoordinateQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.City) || string.IsNullOrWhiteSpace(query.StateCode))
            return NotFound(AddressCoordinateFailureReasons.InsufficientData,
                "Município e UF são obrigatórios para validar a coordenada.");

        EnsureApiKeyConfigured();
        var unsuccessfulReasons = new List<string>();
        var street = NominatimAddressCoordinateProvider.FormatStreet(query.StreetType, query.Street);
        var postalCode = NominatimAddressCoordinateProvider.FormatPostalCode(query.PostalCode);

        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(query.Number))
        {
            var exact = await SearchAsync([street, query.Number, query.Neighborhood, query.City,
                query.StateCode, postalCode, "Brasil"], query, requireHouseNumber: true,
                AddressCoordinateMatchLevels.Exact, cancellationToken);
            if (exact.Status is "RESOLVED" or "FAILED") return exact;
            AddReason(exact);
        }
        if (string.IsNullOrWhiteSpace(query.Number) && !string.IsNullOrWhiteSpace(postalCode))
        {
            var postalCodeApproximation = await SearchPostalCodeAsync(postalCode, query, cancellationToken);
            if (postalCodeApproximation.Status is "RESOLVED" or "FAILED") return postalCodeApproximation;
            AddReason(postalCodeApproximation);
        }
        if (!string.IsNullOrWhiteSpace(street))
        {
            var streetApproximation = await SearchAsync([street, query.Neighborhood, query.City,
                query.StateCode, postalCode, "Brasil"], query, requireHouseNumber: false,
                AddressCoordinateMatchLevels.Street, cancellationToken);
            if (streetApproximation.Status is "RESOLVED" or "FAILED") return streetApproximation;
            AddReason(streetApproximation);
        }
        if (!string.IsNullOrWhiteSpace(query.Number) && !string.IsNullOrWhiteSpace(postalCode))
        {
            var postalCodeApproximation = await SearchPostalCodeAsync(postalCode, query, cancellationToken);
            if (postalCodeApproximation.Status is "RESOLVED" or "FAILED") return postalCodeApproximation;
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

    private async Task<AddressCoordinateLookup> SearchAsync(
        IEnumerable<string?> parts,
        AddressCoordinateQuery query,
        bool requireHouseNumber,
        string matchLevel,
        CancellationToken cancellationToken)
    {
        var address = string.Join(", ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        var url = "v1/geocode/search?text=" + Uri.EscapeDataString(address) +
            "&filter=countrycode:br&limit=5&format=json&apiKey=" + Uri.EscapeDataString(geoapifyOptions.Value.ApiKey);
        using var response = await SendWithTransportRetryAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date is { } retryAt ? retryAt - DateTimeOffset.UtcNow : null);
            if (retryAfter > TimeSpan.Zero) await Task.Delay(retryAfter.Value, cancellationToken);
            throw new GeoapifyRateLimitException(retryAfter);
        }
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<GeoapifyResponse>(cancellationToken);
        var results = payload?.Results ?? [];
        var compatibleResults = results.Where(result => IsCompatibleMunicipality(result, query)).ToArray();
        var result = requireHouseNumber
            ? compatibleResults.FirstOrDefault(item => SameText(item.HouseNumber, query.Number))
            : compatibleResults.FirstOrDefault();
        if (result is null)
            return results.Length > 0 && compatibleResults.Length == 0
                ? NotFound(AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState,
                    "Os resultados encontrados não correspondem ao município e à UF consultados.")
                : NotFound(AddressCoordinateFailureReasons.ExhaustedFallbacks,
                    requireHouseNumber && compatibleResults.Length > 0
                        ? "O município e a UF conferem, mas o número do imóvel não foi confirmado."
                        : "Nenhum resultado encontrado.");
        if (result.Latitude is null || result.Longitude is null)
            return new("FAILED", null, null, result.PlaceId, result.Formatted,
                "Coordenadas inválidas no retorno.", matchLevel, Source(matchLevel));
        return new("RESOLVED", result.Latitude, result.Longitude, result.PlaceId, result.Formatted,
            ApproximationReason(matchLevel), matchLevel, Source(matchLevel));
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

    private async Task<HttpResponseMessage> SendWithTransportRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        var maximumRetries = Math.Max(0, geoapifyOptions.Value.TransportMaximumRetries);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(0, geoapifyOptions.Value.TransportRetryDelayMilliseconds));
        for (var retry = 0; ; retry++)
        {
            try { return await httpClient.GetAsync(uri, cancellationToken); }
            catch (HttpRequestException) when (retry < maximumRetries)
            {
                if (retryDelay > TimeSpan.Zero) await Task.Delay(retryDelay * (retry + 1), cancellationToken);
            }
        }
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(geoapifyOptions.Value.ApiKey))
            throw new InvalidOperationException("Geoapify:ApiKey não foi configurada.");
    }

    private static bool IsCompatibleMunicipality(GeoapifyResult result, AddressCoordinateQuery query) =>
        SameText(result.City ?? result.Municipality ?? result.County, query.City) &&
        string.Equals(result.StateCode, query.StateCode, StringComparison.OrdinalIgnoreCase);
    private static bool SameText(string? left, string? right) => Normalize(left) == Normalize(right);
    private static string Normalize(string? value) => string.Concat((value ?? string.Empty)
        .Normalize(System.Text.NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Normalize(System.Text.NormalizationForm.FormC).Trim().ToUpperInvariant();
    private static string Source(string matchLevel) => $"GEOAPIFY_{matchLevel}";
    private static string? ApproximationReason(string matchLevel) => matchLevel switch
    {
        AddressCoordinateMatchLevels.Street => "Coordenada aproximada pelo logradouro; número não confirmado.",
        AddressCoordinateMatchLevels.Municipality => "Coordenada aproximada pelo município.",
        _ => null
    };
    private static AddressCoordinateLookup NotFound(string category, string detail) =>
        new("NOT_FOUND", null, null, null, null, $"{category} {detail}", Source: "GEOAPIFY");

    private sealed record GeoapifyResponse([property: JsonPropertyName("results")] GeoapifyResult[] Results);
    private sealed record GeoapifyResult(
        [property: JsonPropertyName("place_id")] string? PlaceId,
        [property: JsonPropertyName("lat")] decimal? Latitude,
        [property: JsonPropertyName("lon")] decimal? Longitude,
        [property: JsonPropertyName("formatted")] string? Formatted,
        [property: JsonPropertyName("housenumber")] string? HouseNumber,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("county")] string? County,
        [property: JsonPropertyName("state_code")] string? StateCode);
    private sealed record BrasilApiPostalCodeResult(
        [property: JsonPropertyName("cep")] string? Cep,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("neighborhood")] string? Neighborhood,
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("location")] BrasilApiLocation? Location);
    private sealed record BrasilApiLocation([property: JsonPropertyName("coordinates")] BrasilApiCoordinates? Coordinates);
    private sealed record BrasilApiCoordinates(
        [property: JsonPropertyName("longitude")] string? Longitude,
        [property: JsonPropertyName("latitude")] string? Latitude);
}

public sealed class GeoapifyRateLimitException(TimeSpan? retryAfter)
    : Exception("O Geoapify limitou temporariamente as requisições.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
