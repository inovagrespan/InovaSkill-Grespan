using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using System.Reflection;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class CommercialTransactionBufferTests
{
    [Fact]
    public void Add_RecalculatesTotalAmountFromQuantityAndUnitPrice_IgnoringInputTotal()
    {
        var row = new ImportedRow(10, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "0001",
            ["transactiondate"] = "2025-07-31",
            ["customercode"] = "C1",
            ["customername"] = "Empresa X",
            ["productcode"] = "P1",
            ["productdescription"] = "Produto X",
            ["quantity"] = "2",
            ["unitprice"] = "10.5",
            ["totalamount"] = "-9999.99",
            ["transactiontype"] = "N",
            ["city"] = "SP",
            ["productgroup"] = "G1",
            ["grossweightkg"] = "1.2"
        });

        var buffer = new CommercialTransactionBuffer();
        buffer.Add(row, 77);

        var itemsField = typeof(CommercialTransactionBuffer)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(itemsField);

        var items = Assert.IsType<List<CommercialTransaction>>(itemsField!.GetValue(buffer));
        var stored = Assert.Single(items);

        Assert.Equal(2m, stored.Quantity);
        Assert.Equal(10.5m, stored.UnitPrice);
        Assert.Equal(21m, stored.TotalAmount);
    }

    [Fact]
    public void Add_IgnoresDuplicateCommercialTransactionInSameBuffer()
    {
        var row = new ImportedRow(10, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "0001",
            ["transactiondate"] = "2025-07-31",
            ["customercode"] = "C1",
            ["customername"] = "Empresa X",
            ["productcode"] = "P1",
            ["productdescription"] = "Produto X",
            ["quantity"] = "2",
            ["unitprice"] = "10.5",
            ["totalamount"] = "21",
            ["transactiontype"] = "N",
            ["city"] = "SP",
            ["productgroup"] = "G1",
            ["grossweightkg"] = "1.2"
        });

        var buffer = new CommercialTransactionBuffer();
        buffer.Add(row, 77);
        buffer.Add(row, 77);

        var itemsField = typeof(CommercialTransactionBuffer)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(itemsField);

        var items = Assert.IsType<List<CommercialTransaction>>(itemsField!.GetValue(buffer));
        Assert.Single(items);
    }

    [Fact]
    public void Add_AcceptsBrazilianDateTimeWithTime()
    {
        var row = new ImportedRow(10, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "0001",
            ["transactiondate"] = "31/07/2025 00:00:00",
            ["customercode"] = "C1",
            ["customername"] = "Empresa X",
            ["productcode"] = "P1",
            ["productdescription"] = "Produto X",
            ["quantity"] = "2",
            ["unitprice"] = "10.5",
            ["totalamount"] = "21",
            ["transactiontype"] = "N",
            ["city"] = "SP",
            ["productgroup"] = "G1",
            ["grossweightkg"] = "1.2"
        });

        var buffer = new CommercialTransactionBuffer();
        buffer.Add(row, 77);

        var itemsField = typeof(CommercialTransactionBuffer)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(itemsField);

        var items = Assert.IsType<List<CommercialTransaction>>(itemsField!.GetValue(buffer));
        var stored = Assert.Single(items);

        Assert.Equal(new DateTime(2025, 7, 31, 0, 0, 0, DateTimeKind.Utc), stored.TransactionDate);
    }
}
