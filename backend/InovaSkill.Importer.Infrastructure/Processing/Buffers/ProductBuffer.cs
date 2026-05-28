using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class ProductBuffer : IFileTypeBuffer
{
    private readonly List<Product> _items = [];

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        _items.Add(new Product
        {
            Sku = row.Get("sku"),
            Name = row.Get("name"),
            Price = decimal.Parse(row.Get("price")),
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
