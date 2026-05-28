namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileParserFactory
{
    IFileParser Create(string filePath);
}
