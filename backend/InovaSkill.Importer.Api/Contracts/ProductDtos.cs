namespace InovaSkill.Importer.Api.Contracts;

public sealed record ProductDto(
    long Id,
    string Sku,
    string Name,
    decimal Price,
    DateTime CreatedAt,
    long SourceFileJobId);
