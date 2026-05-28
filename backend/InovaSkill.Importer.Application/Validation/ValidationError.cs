namespace InovaSkill.Importer.Application.Validation;

public sealed record ValidationError(string Column, string Message);
