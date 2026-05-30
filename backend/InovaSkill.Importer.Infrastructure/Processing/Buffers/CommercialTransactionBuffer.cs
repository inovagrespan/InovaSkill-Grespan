using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class CommercialTransactionBuffer : IFileTypeBuffer
{
    private readonly List<CommercialTransaction> _items = [];

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        var quantity = decimal.Parse(row.Get("quantity"), CultureInfo.InvariantCulture);
        var unitPrice = decimal.Parse(row.Get("unitprice"), CultureInfo.InvariantCulture);
        var grossWeightKg = decimal.Parse(row.Get("grossweightkg"), CultureInfo.InvariantCulture);
        var totalAmount = Math.Abs(quantity * unitPrice);

        _items.Add(new CommercialTransaction
        {
            DocumentNumber = row.Get("documentnumber"),
            TransactionDate = UtcDateTimeParser.ParseRequired(row.Get("transactiondate")),
            CustomerCode = row.Get("customercode"),
            CustomerName = row.Get("customername"),
            ProductCode = row.Get("productcode"),
            ProductDescription = row.Get("productdescription"),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = totalAmount,
            TransactionType = row.Get("transactiontype"),
            City = row.Get("city"),
            ProductGroup = row.Get("productgroup"),
            GrossWeightKg = grossWeightKg,
            SourceFileJobId = sourceFileJobId
        });
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.BulkInsertAsync(_items, cancellationToken: cancellationToken);
        _items.Clear();
    }
}

