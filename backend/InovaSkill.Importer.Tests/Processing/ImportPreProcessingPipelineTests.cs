using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Mappings;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Processing.TransformRules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace InovaSkill.Importer.Tests.Processing;

public class ImportPreProcessingPipelineTests
{
    [Fact]
    public async Task ProcessRowsAsync_MapsTemplateRowsFromMemory()
    {
        var template = new ImportTemplate
        {
            ImportFileType = new ImportFileType { Code = ImportFileTypeCodes.SalesInvoice },
            ColumnMappings =
            [
                BuildMapping("documento", "documentnumber"),
                BuildMapping("total", "totalamount")
            ]
        };
        var pipeline = BuildPipeline(template);

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("vendas.csv", null, Rows(
                new TableRow(2, new Dictionary<string, string>
                {
                    ["documento"] = " 000123 ",
                    ["total"] = "83.00"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.Empty(result[0].Errors);
        Assert.False(result[0].ShouldStopProcessing);
        Assert.Equal(ImportFileTypeCodes.SalesInvoice, result[0].DetectedFileTypeCode);
        Assert.Equal("000123", result[0].Row.Get("documentnumber"));
        Assert.Equal("83.00", result[0].Row.Get("totalamount"));
    }

    [Fact]
    public async Task ProcessRowsAsync_FormatsMappedDecimalValuesWithInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");

        try
        {
            var template = new ImportTemplate
            {
                ImportFileType = new ImportFileType { Code = ImportFileTypeCodes.SalesInvoice },
                ColumnMappings =
                [
                    BuildMapping("vlr. unit.", "unitprice", "BrazilianCurrency", """{"culture":"pt-BR"}"""),
                    BuildMapping("data", "transactiondate", "BrazilianDate", """{"formats":["dd/MM/yyyy"]}""")
                ]
            };
            var pipeline = BuildPipeline(template, [new TrimRule(), new BrazilianCurrencyRule(), new BrazilianDateRule()]);

            var result = await CollectAsync(pipeline.ProcessRowsAsync(
                new ImportPreProcessingRequest("vendas.csv", null, Rows(
                    new TableRow(2, new Dictionary<string, string>
                    {
                        ["vlr. unit."] = "2824,51447619048",
                        ["data"] = "04/07/2025"
                    }))),
                CancellationToken.None));

            Assert.Single(result);
            Assert.Empty(result[0].Errors);
            Assert.Equal("2824.51447619048", result[0].Row.Get("unitprice"));
            Assert.Equal("2025-07-04", result[0].Row.Get("transactiondate"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task ProcessRowsAsync_AppliesCustomerAliasesWithoutTemplate()
    {
        var pipeline = BuildPipeline(template: null);

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("clientes.csv", ImportFileTypeCodes.Customers, Rows(
                new TableRow(2, new Dictionary<string, string>
                {
                    ["cliente"] = "C-001",
                    ["nome"] = "Ana Silva",
                    ["e-mail"] = "ana@corp.com"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.Empty(result[0].Errors);
        Assert.Equal("C-001", result[0].Row.Get("customercode"));
        Assert.Equal("Ana Silva", result[0].Row.Get("name"));
        Assert.Equal("ana@corp.com", result[0].Row.Get("email"));
    }

    [Fact]
    public async Task ProcessRowsAsync_MapsRecognizedPatternAliasesWithoutDynamicTemplateConfiguration()
    {
        var services = new ServiceCollection();
        services.AddImportInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());
        using var provider = services.BuildServiceProvider();

        var pipeline = new ImportPreProcessingPipeline(
            provider.GetRequiredService<IPreProcessorTemplateResolver>(),
            new ImportMappingEngine(new TransformRuleRegistry([new TrimRule(), new BrazilianCurrencyRule(), new BrazilianDateRule()])),
            provider.GetRequiredService<IFileTypeDetector>());

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("ITEM DE VENDA.xlsx", null, Rows(
                new TableRow(2, new Dictionary<string, string>
                {
                    ["documento"] = " NF-001 ",
                    ["data"] = "20260530",
                    ["cliente"] = "C-10",
                    ["nome"] = "Cliente Exemplo",
                    ["produto"] = "P-200",
                    ["descricao"] = "Produto Teste",
                    ["quantidade"] = "2,500",
                    ["vlr. unit."] = "12,40",
                    ["total"] = "31,00",
                    ["cidade"] = "Campinas",
                    ["grupo descricao"] = "Bebidas",
                    ["peso bruto(kg)"] = "1,250"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.Empty(result[0].Errors);
        Assert.Equal(ImportFileTypeCodes.SalesInvoice, result[0].DetectedFileTypeCode);
        Assert.Equal("NF-001", result[0].Row.Get("documentnumber"));
        Assert.Equal("2026-05-30", result[0].Row.Get("transactiondate"));
        Assert.Equal("31.00", result[0].Row.Get("totalamount"));
    }

    [Fact]
    public async Task ProcessRowsAsync_MapsTotvsCustomerHeadersToCanonicalFields()
    {
        var services = new ServiceCollection();
        services.AddImportInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());
        using var provider = services.BuildServiceProvider();

        var pipeline = new ImportPreProcessingPipeline(
            provider.GetRequiredService<IPreProcessorTemplateResolver>(),
            new ImportMappingEngine(new TransformRuleRegistry([new TrimRule(), new BrazilianCurrencyRule(), new BrazilianDateRule()])),
            provider.GetRequiredService<IFileTypeDetector>());

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("cod clientes.xlsx", null, Rows(
                new TableRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cod totvs"] = "1",
                    ["cliente (consumo bruto (vendas+bonif)"] = "NSA AREALVA"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.Empty(result[0].Errors);
        Assert.False(result[0].ShouldStopProcessing);
        Assert.Equal(ImportFileTypeCodes.Customers, result[0].DetectedFileTypeCode);
        Assert.Equal("1", result[0].Row.Get("customercode"));
        Assert.Equal("NSA AREALVA", result[0].Row.Get("name"));
    }

    [Fact]
    public async Task ProcessRowsAsync_MapsBrowseProductHeadersToCanonicalFields()
    {
        var services = new ServiceCollection();
        services.AddImportInfrastructure(new ConfigurationBuilder().AddInMemoryCollection().Build());
        using var provider = services.BuildServiceProvider();

        var pipeline = new ImportPreProcessingPipeline(
            provider.GetRequiredService<IPreProcessorTemplateResolver>(),
            new ImportMappingEngine(new TransformRuleRegistry([new TrimRule(), new BrazilianCurrencyRule(), new BrazilianDateRule()])),
            provider.GetRequiredService<IFileTypeDetector>());

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("Cadastro de produtos.xlsx", null, Rows(
                new TableRow(3, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["codigo"] = "10000001",
                    ["descricao"] = "AMOSTRA BAGUETE GRANDE PCT COM 2 UND COM 660G",
                    ["ult. preco"] = "0"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.Empty(result[0].Errors);
        Assert.False(result[0].ShouldStopProcessing);
        Assert.Equal(ImportFileTypeCodes.Products, result[0].DetectedFileTypeCode);
        Assert.Equal("10000001", result[0].Row.Get("sku"));
        Assert.Equal("AMOSTRA BAGUETE GRANDE PCT COM 2 UND COM 660G", result[0].Row.Get("name"));
        Assert.Equal("0", result[0].Row.Get("price"));
    }

    [Fact]
    public async Task ProcessRowsAsync_ReturnsStopResultWhenFileTypeCannotBeDetected()
    {
        var pipeline = BuildPipeline(template: null);

        var result = await CollectAsync(pipeline.ProcessRowsAsync(
            new ImportPreProcessingRequest("desconhecido.csv", null, Rows(
                new TableRow(2, new Dictionary<string, string>
                {
                    ["foo"] = "bar",
                    ["baz"] = "qux"
                }),
                new TableRow(3, new Dictionary<string, string>
                {
                    ["foo"] = "outro",
                    ["baz"] = "valor"
                }))),
            CancellationToken.None));

        Assert.Single(result);
        Assert.True(result[0].ShouldStopProcessing);
        Assert.Contains(result[0].Errors, error => error.Column == "ImportFileType");
    }

    private static ImportPreProcessingPipeline BuildPipeline(ImportTemplate? template)
    {
        return BuildPipeline(template, [new TrimRule()]);
    }

    private static ImportPreProcessingPipeline BuildPipeline(ImportTemplate? template, IReadOnlyList<ITransformRule> rules)
    {
        return new ImportPreProcessingPipeline(
            new StubTemplateResolver(template),
            new ImportMappingEngine(new TransformRuleRegistry(rules)),
            new StubFileTypeDetector());
    }

    private static ImportColumnMapping BuildMapping(string sourceColumnName, string targetFieldName)
    {
        return BuildMapping(sourceColumnName, targetFieldName, "Trim", null);
    }

    private static ImportColumnMapping BuildMapping(
        string sourceColumnName,
        string targetFieldName,
        string ruleCode,
        string? parametersJson)
    {
        return new ImportColumnMapping
        {
            SourceColumnName = sourceColumnName,
            TargetFieldName = targetFieldName,
            IsRequired = true,
            TransformRules =
            [
                new ColumnMappingTransformRule
                {
                    Order = 1,
                    ParametersJson = parametersJson,
                    TransformRule = new TransformRule { Code = ruleCode }
                }
            ]
        };
    }

    private static async Task<List<PreProcessedImportRow>> CollectAsync(IAsyncEnumerable<PreProcessedImportRow> rows)
    {
        var result = new List<PreProcessedImportRow>();
        await foreach (var row in rows)
        {
            result.Add(row);
        }

        return result;
    }

    private static async IAsyncEnumerable<TableRow> Rows(params TableRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
            await Task.Yield();
        }
    }

    private sealed class StubTemplateResolver(ImportTemplate? template) : IPreProcessorTemplateResolver
    {
        public Task<ImportTemplate?> ResolveAsync(string fileName, IReadOnlyCollection<string> headers, CancellationToken cancellationToken)
        {
            return Task.FromResult(template);
        }
    }

    private sealed class StubFileTypeDetector : IFileTypeDetector
    {
        public string? DetectCode(IReadOnlyDictionary<string, string> row)
        {
            if (row.ContainsKey("documentnumber") || row.ContainsKey("documento"))
            {
                return ImportFileTypeCodes.SalesInvoice;
            }

            if (row.ContainsKey("customercode") || row.ContainsKey("cliente"))
            {
                return ImportFileTypeCodes.Customers;
            }

            return row.ContainsKey("sku") || row.ContainsKey("codigo")
                ? ImportFileTypeCodes.Products
                : null;
        }
    }
}
