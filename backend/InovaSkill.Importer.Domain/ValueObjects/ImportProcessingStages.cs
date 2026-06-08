namespace InovaSkill.Importer.Domain.ValueObjects;

public static class ImportProcessingStages
{
    public const string PreProcessing = "PRE_PROCESSING";
    public const string Validation = "VALIDATION";
    public const string Import = "IMPORT";

    public static readonly IReadOnlyList<ImportProcessingStageDefinition> All =
    [
        new(PreProcessing, "Pré-processamento"),
        new(Validation, "Validação"),
        new(Import, "Processamento")
    ];
}

public sealed record ImportProcessingStageDefinition(string Code, string Name);
