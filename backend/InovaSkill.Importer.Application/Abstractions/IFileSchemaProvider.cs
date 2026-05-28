using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileSchemaProvider
{
    FileSchema GetSchema(FileType fileType);
}
