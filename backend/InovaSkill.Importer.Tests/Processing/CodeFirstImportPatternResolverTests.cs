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
        Assert.Equal(ImportFileTypeCodes.ProductList, template!.ImportFileType?.Code);
    }
}
