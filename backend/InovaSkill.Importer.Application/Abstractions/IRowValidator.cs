using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IRowValidator
{
    InovaSkill.Importer.Application.Validation.ValidationResult Validate(ImportedRow row, FileSchema schema);
}
