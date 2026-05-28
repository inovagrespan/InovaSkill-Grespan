using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileTypeDetector
{
    FileType Detect(IReadOnlyDictionary<string, string> row);
}
