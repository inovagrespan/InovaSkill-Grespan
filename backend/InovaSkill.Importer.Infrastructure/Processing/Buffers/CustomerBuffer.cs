using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class CustomerBuffer : IFileTypeBuffer
{
    private readonly List<Customer> _items = [];

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        _items.Add(new Customer
        {
            Name = row.Get("name"),
            Email = row.Get("email"),
            CreatedAt = UtcDateTimeParser.ParseOrDefaultUtcNow(row.Get("createdat")),
            SourceFileJobId = sourceFileJobId
        });
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.BulkInsertAsync(_items, cancellationToken: cancellationToken);
        _items.Clear();
    }
}
