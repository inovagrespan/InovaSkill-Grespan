using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class CommercialTransactionBuffer : IFileTypeBuffer
{
    private readonly List<CommercialTransaction> _items = [];
    private readonly HashSet<string> _businessKeys = new(StringComparer.Ordinal);

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        var quantity = decimal.Parse(row.Get("quantity"), CultureInfo.InvariantCulture);
        var unitPrice = decimal.Parse(row.Get("unitprice"), CultureInfo.InvariantCulture);
        var grossWeightKg = decimal.Parse(row.Get("grossweightkg"), CultureInfo.InvariantCulture);
        var totalAmount = Math.Abs(quantity * unitPrice);

        var item = new CommercialTransaction
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
        };

        // Evita duplicidades no mesmo lote/arquivo antes de persistir.
        if (_businessKeys.Add(BuildBusinessKey(item)))
        {
            _items.Add(item);
        }
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var existingKeys = new HashSet<string>(StringComparer.Ordinal);
        var documents = _items.Select(x => x.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (documents.Count > 0)
        {
            var minDate = _items.Min(x => x.TransactionDate);
            var maxDate = _items.Max(x => x.TransactionDate);

            var candidates = await dbContext.CommercialTransactions
                .AsNoTracking()
                .Where(x =>
                    documents.Contains(x.DocumentNumber) &&
                    x.TransactionDate >= minDate &&
                    x.TransactionDate <= maxDate)
                .ToListAsync(cancellationToken);

            foreach (var existing in candidates)
            {
                existingKeys.Add(BuildBusinessKey(existing));
            }
        }

        var newItems = _items.Where(x => !existingKeys.Contains(BuildBusinessKey(x))).ToList();
        if (newItems.Count > 0)
        {
            await dbContext.BulkInsertAsync(newItems, cancellationToken: cancellationToken);
        }

        _items.Clear();
        _businessKeys.Clear();
    }

    private static string BuildBusinessKey(CommercialTransaction item)
    {
        static string NormalizeText(string value) => value.Trim().ToUpperInvariant();

        return string.Join("|",
            NormalizeText(item.DocumentNumber),
            item.TransactionDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            NormalizeText(item.CustomerCode),
            NormalizeText(item.ProductCode),
            NormalizeText(item.TransactionType),
            NormalizeText(item.City),
            NormalizeText(item.ProductGroup),
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            item.UnitPrice.ToString(CultureInfo.InvariantCulture),
            item.GrossWeightKg.ToString(CultureInfo.InvariantCulture));
    }
}

