using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Tests.Processing;

public class CodeFirstImportPatternResolverTests
{
    private readonly IPreProcessorTemplateResolver _resolver;

    public CodeFirstImportPatternResolverTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddImportInfrastructure(configuration);
        _resolver = services.BuildServiceProvider().GetRequiredService<IPreProcessorTemplateResolver>();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsTemplate_WhenSheetMatchesKnownPattern()
    {
        var template = await _resolver.ResolveAsync(
            "ITEM DE VENDA(NOTAS FISCAIS DE SAIDA).xlsx",
            ["documento", "data", "cliente", "nome", "produto", "descricao", "quantidade", "vlr. unit.", "total", "cidade", "grupo descricao", "peso bruto(kg)"],
            CancellationToken.None);

        Assert.NotNull(template);
        Assert.Equal(ImportFileTypeCodes.SalesInvoice, template!.ImportFileType?.Code);
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "transactiondate" && mapping.SourceColumnName == "data");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_WhenRequiredColumnsAreMissing()
    {
        var template = await _resolver.ResolveAsync(
            "clientes.xlsx",
            ["nome", "email"],
            CancellationToken.None);

        Assert.Null(template);
    }

    [Fact]
    public async Task ResolveAsync_SelectsBestPattern_WhenMoreThanOneCandidateExists()
    {
        var template = await _resolver.ResolveAsync(
            "produtos_com_email.xlsx",
            ["codigo produto", "descricao", "valor", "email"],
            CancellationToken.None);

        Assert.NotNull(template);
        Assert.Equal(ImportFileTypeCodes.Products, template!.ImportFileType?.Code);
    }

    [Fact]
    public async Task ResolveAsync_UsesActualSpreadsheetHeaders_WhenCustomerSheetMatchesTotvsAliases()
    {
        var template = await _resolver.ResolveAsync(
            "cod clientes.xlsx",
            ["COD TOTVS", "Cliente (CONSUMO BRUTO (VENDAS+BONIF)"],
            CancellationToken.None);

        Assert.NotNull(template);
        Assert.Equal(ImportFileTypeCodes.Customers, template!.ImportFileType?.Code);
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "customercode" && mapping.SourceColumnName == "COD TOTVS");
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "name" && mapping.SourceColumnName == "Cliente (CONSUMO BRUTO (VENDAS+BONIF)");
    }

    [Fact]
    public async Task ResolveAsync_UsesActualSpreadsheetHeaders_WhenProductSheetMatchesBrowseLayout()
    {
        var template = await _resolver.ResolveAsync(
            "Cadastro de produtos.xlsx",
            ["Codigo", "*Cod.OnClick", "Descricao", "Descr.Espec.", "Ult. Preco"],
            CancellationToken.None);

        Assert.NotNull(template);
        Assert.Equal(ImportFileTypeCodes.Products, template!.ImportFileType?.Code);
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "sku" && mapping.SourceColumnName == "Codigo");
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "name" && mapping.SourceColumnName == "Descricao");
        Assert.Contains(template.ColumnMappings, mapping => mapping.TargetFieldName == "price" && mapping.SourceColumnName == "Ult. Preco");
    }
}
