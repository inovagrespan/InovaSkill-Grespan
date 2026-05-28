namespace InovaSkill.Importer.Application.Validation;

public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = [];
}
