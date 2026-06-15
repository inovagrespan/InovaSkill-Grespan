using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Tests.Processing;

public class FileTypeDetectorTests
{
    private readonly IFileTypeDetector _sut;

    public FileTypeDetectorTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddImportInfrastructure(configuration);
        _sut = services.BuildServiceProvider().GetRequiredService<IFileTypeDetector>();
    }

    [Fact]
    public void Detect_ReturnsCustomers_WhenRequiredAliasesMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cliente"] = "C-01",
            ["nome"] = "Alice"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.Customers, result);
    }

    [Fact]
    public void Detect_ReturnsCommercialTransaction_WhenPortugueseAliasesMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documento"] = "000001",
            ["data"] = "20260530",
            ["cliente"] = "001091",
            ["nome"] = "Mercado XPTO",
            ["produto"] = "10000164",
            ["descricao"] = "Produto A",
            ["quantidade"] = "2,5",
            ["vlr. unit."] = "10,00",
            ["total"] = "25,00",
            ["cidade"] = "Campinas",
            ["grupo descricao"] = "Bebidas",
            ["peso bruto(kg)"] = "15,40"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.SalesInvoice, result);
    }

    [Fact]
    public void Detect_ReturnsNull_WhenRequiredColumnsAreMissing()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nome"] = "Cliente sem codigo",
            ["email"] = "cliente@corp.com"
        };

        var result = _sut.DetectCode(row);

        Assert.Null(result);
    }

    [Fact]
    public void Detect_ReturnsCustomers_WhenTotvsCustomerSpreadsheetHeadersMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cod totvs"] = "1",
            ["cliente (consumo bruto (vendas+bonif)"] = "NSA AREALVA"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.Customers, result);
    }

    [Fact]
    public void Detect_ReturnsProducts_WhenBrowseProductHeadersMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["codigo"] = "10000001",
            ["descricao"] = "AMOSTRA BAGUETE GRANDE",
            ["ult. preco"] = "0"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.Products, result);
    }
}
