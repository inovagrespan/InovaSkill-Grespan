namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileTypeDetector
{
    string? DetectCode(IReadOnlyDictionary<string, string> row);
}
