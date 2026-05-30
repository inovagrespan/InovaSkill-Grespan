using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class CustomerBufferTests
{
    [Fact]
    public async Task FlushAsync_MapsSalesAliasesToCustomerFields()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var buffer = new CustomerBuffer();
        buffer.Add(new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cliente"] = "CLI-100",
            ["nome"] = "Cliente Via Vendas"
        }), 20);

        await buffer.FlushAsync(db, CancellationToken.None);
        var customer = await db.Customers.SingleAsync();

        Assert.Equal("CLI-100", customer.CustomerCode);
        Assert.Equal("Cliente Via Vendas", customer.Name);
        Assert.Equal("cli-100@import.local", customer.Email);
    }

    [Fact]
    public async Task FlushAsync_IgnoresDuplicateCustomerCode_InBufferAndDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Customers.Add(new Customer
        {
            CustomerCode = "C-EXISTENTE",
            Name = "Cliente Existente",
            Email = "existente@import.local",
            SourceFileJobId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var buffer = new CustomerBuffer();

        buffer.Add(new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customercode"] = "C-001",
            ["name"] = "Cliente A",
            ["email"] = "cliente.a@empresa.com",
            ["createdat"] = "2026-01-01"
        }), 10);

        buffer.Add(new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customercode"] = "C-001",
            ["name"] = "Cliente A Duplicado",
            ["email"] = "duplicado@empresa.com",
            ["createdat"] = "2026-01-02"
        }), 10);

        buffer.Add(new ImportedRow(3, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customercode"] = "C-EXISTENTE",
            ["name"] = "Cliente Existente Novo",
            ["email"] = "novo@empresa.com",
            ["createdat"] = "2026-01-03"
        }), 10);

        await buffer.FlushAsync(db, CancellationToken.None);

        var customers = await db.Customers
            .OrderBy(x => x.CustomerCode)
            .ToListAsync();

        Assert.Equal(2, customers.Count);
        Assert.Contains(customers, x => x.CustomerCode == "C-001");
        Assert.Contains(customers, x => x.CustomerCode == "C-EXISTENTE");
    }
}
