using EFCore.BulkExtensions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

public sealed class CustomerBuffer : IFileTypeBuffer
{
    private readonly List<Customer> _items = [];

    public int Count => _items.Count;

    public void Add(ImportedRow row, long sourceFileJobId)
    {
        var customerCode = row.Get("customercode").Trim();
        var name = row.Get("name");
        var email = row.Get("email").Trim();

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            customerCode = row.Get("cliente").Trim();
        }

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            customerCode = row.Get("name").Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = row.Get("nome");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            email = row.Get("e-mail").Trim();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            var normalizedCode = customerCode
                .ToLowerInvariant()
                .Replace(" ", ".")
                .Replace("@", string.Empty);
            email = $"{normalizedCode}@import.local";
        }

        _items.Add(new Customer
        {
            CustomerCode = customerCode,
            Name = name,
            Email = email,
            CreatedAt = UtcDateTimeParser.ParseOrDefaultUtcNow(row.Get("createdat")),
            SourceFileJobId = sourceFileJobId
        });
    }

    public async Task FlushAsync(ImportDbContext dbContext, CancellationToken cancellationToken)
    {
        var uniqueByCode = _items
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerCode))
            .GroupBy(x => x.CustomerCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (uniqueByCode.Count == 0)
        {
            _items.Clear();
            return;
        }

        var codes = uniqueByCode
            .Select(x => x.CustomerCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingCodes = await dbContext.Customers
            .AsNoTracking()
            .Where(x => codes.Contains(x.CustomerCode))
            .Select(x => x.CustomerCode)
            .ToListAsync(cancellationToken);

        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toInsert = uniqueByCode
            .Where(x => !existingSet.Contains(x.CustomerCode))
            .ToList();

        if (toInsert.Count > 0)
        {
            await dbContext.BulkInsertAsync(toInsert, cancellationToken: cancellationToken);
        }

        _items.Clear();
    }
}
