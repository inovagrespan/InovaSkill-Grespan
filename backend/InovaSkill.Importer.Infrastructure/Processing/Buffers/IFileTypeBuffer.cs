using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public interface IFileTypeBuffer
{
    int Count { get; }
    void Add(ImportedRow row, long sourceFileJobId);
    Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken);
}
