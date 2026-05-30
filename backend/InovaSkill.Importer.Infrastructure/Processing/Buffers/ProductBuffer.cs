using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var uniqueBySku = _items
            .Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .GroupBy(x => x.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (uniqueBySku.Count == 0)
        {
            _items.Clear();
            return;
        }

        var skus = uniqueBySku.Select(x => x.Sku.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = await dbContext.Products.AsNoTracking()
            .Where(x => skus.Contains(x.Sku))
            .Select(x => x.Sku)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toInsert = uniqueBySku.Where(x => !existingSet.Contains(x.Sku)).ToList();

        if (toInsert.Count > 0)
        {
            await dbContext.BulkInsertAsync(toInsert, cancellationToken: cancellationToken);
        }

        _items.Clear();
    }
}
