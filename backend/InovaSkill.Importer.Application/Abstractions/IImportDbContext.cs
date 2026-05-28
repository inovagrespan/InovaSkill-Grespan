using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IImportDbContext
{
    IQueryable<FileJob> FileJobs { get; }
}
