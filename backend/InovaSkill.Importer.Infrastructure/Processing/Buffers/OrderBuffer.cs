using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
            OrderedAt = UtcDateTimeParser.ParseRequired(row.Get("orderedat")),
            SourceFileJobId = sourceFileJobId
        });
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        var uniqueOrders = _items
            .GroupBy(x => BuildBusinessKey(x), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        if (uniqueOrders.Count == 0)
        {
            _items.Clear();
            return;
        }

        var existingKeys = await dbContext.Orders.AsNoTracking()
            .Select(x => new
            {
                x.OrderNumber,
                x.CustomerEmail,
                x.ProductSku,
                x.OrderedAt
            })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(x => BuildBusinessKey(x.OrderNumber, x.CustomerEmail, x.ProductSku, x.OrderedAt))
            .ToHashSet(StringComparer.Ordinal);

        var toInsert = uniqueOrders
            .Where(x => !existingSet.Contains(BuildBusinessKey(x)))
            .ToList();

        if (toInsert.Count > 0)
        {
            await dbContext.BulkInsertAsync(toInsert, cancellationToken: cancellationToken);
        }

        _items.Clear();
    }

    private static string BuildBusinessKey(Order row)
    {
        return BuildBusinessKey(row.OrderNumber, row.CustomerEmail, row.ProductSku, row.OrderedAt);
    }

    private static string BuildBusinessKey(string orderNumber, string customerEmail, string productSku, DateTime orderedAt)
    {
        var normalizedAt = orderedAt.Kind == DateTimeKind.Local
            ? orderedAt.ToUniversalTime()
            : DateTime.SpecifyKind(orderedAt, DateTimeKind.Utc);

        return $"{orderNumber.Trim().ToUpperInvariant()}|{customerEmail.Trim().ToUpperInvariant()}|{productSku.Trim().ToUpperInvariant()}|{normalizedAt.Ticks}";
    }
}
