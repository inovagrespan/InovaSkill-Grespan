using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AnalyticsFinanceiroControllerTests
{
    [Fact]
    public async Task Impacto_UsesCommercialTransactionsWhenMaterializedIndicatorsAreMissing()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-A-1", "C1", "Cliente Crescente", new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m),
            Transaction("NF-A-2", "C1", "Cliente Crescente", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), 300m),
            Transaction("NF-A-3", "C1", "Cliente Crescente", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), 400m),
            Transaction("NF-A-4", "C1", "Cliente Crescente", new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), 500m),
            Transaction("NF-B-1", "C2", "Cliente em Queda", new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), 900m),
            Transaction("NF-B-2", "C2", "Cliente em Queda", new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc), 800m),
            Transaction("NF-B-3", "C2", "Cliente em Queda", new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), 700m),
            Transaction("NF-B-4", "C2", "Cliente em Queda", new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc), 300m));
        await db.SaveChangesAsync();

        var controller = new AnalyticsFinanceiroController(db);

        var result = await controller.Impacto(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var resumo = root.GetProperty("resumo");

        Assert.Equal("Cliente em Queda", resumo.GetProperty("maiorCliente").GetString());
        Assert.Equal("Cliente Crescente", resumo.GetProperty("maiorCrescimentoNome").GetString());
        Assert.True(resumo.GetProperty("maiorCrescimentoPct").GetDecimal() > 0);
        Assert.Equal("Cliente em Queda", resumo.GetProperty("maiorQuedaNome").GetString());
        Assert.True(resumo.GetProperty("maiorQuedaPct").GetDecimal() < 0);
        Assert.Contains(root.GetProperty("crescimento").EnumerateArray(), item =>
            item.GetProperty("clienteNome").GetString() == "Cliente Crescente");
        Assert.Contains(root.GetProperty("risco").EnumerateArray(), item =>
            item.GetProperty("clienteNome").GetString() == "Cliente em Queda" &&
            item.GetProperty("tendencia").GetString() == "Queda");
    }

    [Fact]
    public async Task Projecoes_UsesCommercialTransactionsWhenForecastsAreMissing()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-A-1", "C1", "Cliente Crescente", new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m),
            Transaction("NF-A-2", "C1", "Cliente Crescente", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), 300m),
            Transaction("NF-A-3", "C1", "Cliente Crescente", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), 400m),
            Transaction("NF-A-4", "C1", "Cliente Crescente", new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), 500m),
            Transaction("NF-B-1", "C2", "Cliente em Queda", new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), 900m),
            Transaction("NF-B-2", "C2", "Cliente em Queda", new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc), 800m),
            Transaction("NF-B-3", "C2", "Cliente em Queda", new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), 700m),
            Transaction("NF-B-4", "C2", "Cliente em Queda", new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc), 300m));
        await db.SaveChangesAsync();

        var controller = new AnalyticsFinanceiroController(db);

        var result = await controller.Projecoes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("projecoes").GetProperty("proximoMes").GetDecimal() > 0);
        Assert.True(root.GetProperty("projecoes").GetProperty("proximos12Meses").GetDecimal() > root.GetProperty("projecoes").GetProperty("proximoMes").GetDecimal());
        Assert.Contains(root.GetProperty("evolucaoClientes").EnumerateArray(), item =>
            item.GetProperty("clienteNome").GetString() == "Cliente Crescente" &&
            item.GetProperty("valorProjetado").GetDecimal() > item.GetProperty("valorAtual").GetDecimal() &&
            item.GetProperty("TendenciaPrevista").GetString() == "Crescimento");
        Assert.Contains(root.GetProperty("evolucaoClientes").EnumerateArray(), item =>
            item.GetProperty("clienteNome").GetString() == "Cliente em Queda" &&
            item.GetProperty("valorProjetado").GetDecimal() < item.GetProperty("valorAtual").GetDecimal() &&
            item.GetProperty("TendenciaPrevista").GetString() == "Queda");
    }

    private static async Task<ImportDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImportDbContext>().UseSqlite(connection).Options;
        var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static Domain.Entities.CommercialTransaction Transaction(
        string documentNumber,
        string customerCode,
        string customerName,
        DateTime transactionDate,
        decimal totalAmount)
    {
        return new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = documentNumber,
            TransactionDate = transactionDate,
            CustomerCode = customerCode,
            CustomerName = customerName,
            ProductCode = "P1",
            ProductDescription = "Produto",
            Quantity = 1m,
            UnitPrice = totalAmount,
            TotalAmount = totalAmount,
            TransactionType = "Venda",
            City = "Campinas",
            ProductGroup = "Grupo",
            GrossWeightKg = 1m,
            SourceFileJobId = 1
        };
    }
}
