using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.Parsing;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileParserFactory : IFileParserFactory
{
    public IFileParser Create(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => new CsvFileParser(),
            ".xlsx" => new ExcelFileParser(),
            _ => throw new InvalidOperationException($"Unsupported extension: {extension}")
        };
    }
}
