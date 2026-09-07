namespace InovaSkill.Importer.Application.RouteImports;

public sealed record CustomerRegistrationAddressLookup(
    string Status,
    string? PostalCode = null,
    string? StateCode = null,
    string? City = null,
    string? Street = null,
    string? Number = null,
    string? Complement = null,
    string? Neighborhood = null,
    string? StreetType = null,
    string? FailureReason = null,
    string Source = "BRASIL_API_CNPJ",
    string? PostalCodeEnrichmentStatus = null);

public static class PostalCodeEnrichmentStatuses
{
    public const string Complemented = "COMPLEMENTED";
    public const string NotFound = "NOT_FOUND";
    public const string Incompatible = "INCOMPATIBLE";
    public const string TechnicalFailure = "TECHNICAL_FAILURE";
}

public static class CustomerAddressCompleteness
{
    public const string Complete = "COMPLETE";
    public const string WithoutNumber = "WITHOUT_NUMBER";
    public const string PostalOnly = "POSTAL_ONLY";

    public static string From(string? street, string? number) =>
        string.IsNullOrWhiteSpace(street) ? PostalOnly :
        string.IsNullOrWhiteSpace(number) ? WithoutNumber : Complete;
}

public interface ICustomerRegistrationAddressProvider
{
    Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
        string cnpj,
        CancellationToken cancellationToken);
}
