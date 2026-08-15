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
    string? FailureReason = null);

public interface ICustomerRegistrationAddressProvider
{
    Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
        string cnpj,
        CancellationToken cancellationToken);
}
