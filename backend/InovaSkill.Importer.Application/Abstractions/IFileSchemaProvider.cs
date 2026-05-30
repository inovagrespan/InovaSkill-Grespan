using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileSchemaProvider
{
    FileSchema GetSchema(string importFileTypeCode);
}
