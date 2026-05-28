using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class OrderBuffer : IFileTypeBuffer
{
    private readonly List<Order> _items = [];

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        _items.Add(new Order
        {
            OrderNumber = row.Get("ordernumber"),
            CustomerEmail = row.Get("customeremail"),
            ProductSku = row.Get("productsku"),
            Quantity = int.Parse(row.Get("quantity")),
            OrderedAt = DateTime.Parse(row.Get("orderedat")),
            SourceFileJobId = sourceFileJobId
        });
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.BulkInsertAsync(_items, cancellationToken: cancellationToken);
        _items.Clear();
    }
}
