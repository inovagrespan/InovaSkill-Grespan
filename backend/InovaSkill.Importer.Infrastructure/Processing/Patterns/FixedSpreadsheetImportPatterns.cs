using InovaSkill.Importer.Domain.Entities;
using static InovaSkill.Importer.Infrastructure.Processing.Patterns.SpreadsheetImportPatternColumnFactory;

namespace InovaSkill.Importer.Infrastructure.Processing.Patterns;

internal sealed class CustomerSpreadsheetImportPattern : SpreadsheetImportPattern
{
    private static readonly IReadOnlyList<SpreadsheetImportColumnPattern> PatternColumns =
    [
        Text("customercode", "customercode", true, "customer code", "codigo cliente", "codigo do cliente", "cliente", "cod totvs", "codigo totvs"),
        Text("name", "name", true, "nome", "razao social", "cliente nome", "cliente consumo bruto vendas bonif", "cliente consumo bruto"),
        Text("email", "email", false, "e-mail", "mail", "email cliente"),
        Date("createdat", "createdat", false, "created at", "data cadastro", "criado em", "data de cadastro")
    ];

    public override string ImportFileTypeCode => ImportFileTypeCodes.Customers;
    public override string DisplayName => "Clientes";
    protected override IReadOnlyList<string> FileNameHints => ["cliente", "clientes", "customer", "cod clientes", "totvs"];
    protected override IReadOnlyList<SpreadsheetImportColumnPattern> Columns => PatternColumns;
}

internal sealed class ProductSpreadsheetImportPattern : SpreadsheetImportPattern
{
    private static readonly IReadOnlyList<SpreadsheetImportColumnPattern> PatternColumns =
    [
        Text("sku", "sku", true, "codigo", "codigo produto", "codigo do produto", "produto codigo", "product sku"),
        Text("name", "name", true, "nome", "descricao", "descricao produto", "product name"),
        Decimal("price", "price", true, "ult. preco", "ult preco", "ultimo preco", "preco", "preco unitario", "valor", "price"),
        Date("createdat", "createdat", false, "created at", "criado em", "data cadastro")
    ];

    public override string ImportFileTypeCode => ImportFileTypeCodes.Products;
    public override string DisplayName => "Produtos";
    protected override IReadOnlyList<string> FileNameHints => ["produto", "produtos", "cadastro de produtos", "catalogo", "browse", "product"];
    protected override IReadOnlyList<SpreadsheetImportColumnPattern> Columns => PatternColumns;
}

internal sealed class FinancialEntrySpreadsheetImportPattern : SpreadsheetImportPattern
{
    private static readonly IReadOnlyList<SpreadsheetImportColumnPattern> PatternColumns =
    [
        Text("ordernumber", "ordernumber", true, "order number", "numero pedido", "pedido", "numero do pedido"),
        Text("customeremail", "customeremail", true, "email cliente", "e-mail cliente", "customer email"),
        Text("productsku", "productsku", true, "sku produto", "produto sku", "codigo produto", "product sku"),
        Integer("quantity", "quantity", true, "quantidade", "qtd"),
        Date("orderedat", "orderedat", true, "order date", "ordered at", "data pedido", "data do pedido", "orderdate")
    ];

    public override string ImportFileTypeCode => ImportFileTypeCodes.FinancialEntry;
    public override string DisplayName => "Lancamentos Financeiros";
    protected override IReadOnlyList<string> FileNameHints => ["pedido", "pedidos", "order", "orders", "financeiro"];
    protected override IReadOnlyList<SpreadsheetImportColumnPattern> Columns => PatternColumns;
}

internal sealed class SalesInvoiceSpreadsheetImportPattern : SpreadsheetImportPattern
{
    private static readonly IReadOnlyList<SpreadsheetImportColumnPattern> PatternColumns =
    [
        Text("documento", "documentnumber", true, "documentnumber", "document number", "documento fiscal"),
        Date("data", "transactiondate", true, "transactiondate", "transaction date", "data venda", "data emissao"),
        Text("cliente", "customercode", true, "customercode", "customer code", "codigo cliente", "codigo do cliente"),
        Text("nome", "customername", true, "customername", "customer name", "nome cliente"),
        Text("produto", "productcode", true, "productcode", "product code", "codigo produto", "codigo do produto"),
        Text("descricao", "productdescription", true, "productdescription", "product description", "descricao produto"),
        Decimal("quantidade", "quantity", true, "quantity", "qtd"),
        Decimal("vlr. unit.", "unitprice", true, "unitprice", "unit price", "valor unitario", "vlr unit"),
        Decimal("total", "totalamount", true, "totalamount", "total amount", "valor total"),
        Text("tipo", "transactiontype", false, "transactiontype", "transaction type", "tipo operacao"),
        Text("cidade", "city", true, "city", "cidade destino"),
        Text("grupo descricao", "productgroup", true, "productgroup", "grupo produto", "grupo", "grupo descricao produto"),
        Decimal("peso bruto(kg)", "grossweightkg", true, "grossweightkg", "peso bruto", "gross weight kg", "peso bruto kg")
    ];

    public override string ImportFileTypeCode => ImportFileTypeCodes.SalesInvoice;
    public override string DisplayName => "Notas Fiscais de Venda";
    protected override IReadOnlyList<string> FileNameHints => ["item de venda", "nota fiscal", "notas fiscais", "sales invoice", "venda"];
    protected override IReadOnlyList<SpreadsheetImportColumnPattern> Columns => PatternColumns;
}

file static class SpreadsheetImportPatternColumnFactory
{
    public static SpreadsheetImportColumnPattern Text(string sourceColumnName, string targetFieldName, bool isRequired, params string[] acceptedHeaders)
    {
        return new SpreadsheetImportColumnPattern(
            sourceColumnName,
            targetFieldName,
            isRequired,
            acceptedHeaders,
            null,
            [new SpreadsheetImportTransformRuleConfig("Trim")]);
    }

    public static SpreadsheetImportColumnPattern Decimal(string sourceColumnName, string targetFieldName, bool isRequired, params string[] acceptedHeaders)
    {
        return new SpreadsheetImportColumnPattern(
            sourceColumnName,
            targetFieldName,
            isRequired,
            acceptedHeaders,
            null,
            [
                new SpreadsheetImportTransformRuleConfig("Trim"),
                new SpreadsheetImportTransformRuleConfig("BrazilianCurrency", """{"culture":"pt-BR"}""")
            ]);
    }

    public static SpreadsheetImportColumnPattern Integer(string sourceColumnName, string targetFieldName, bool isRequired, params string[] acceptedHeaders)
    {
        return new SpreadsheetImportColumnPattern(
            sourceColumnName,
            targetFieldName,
            isRequired,
            acceptedHeaders,
            null,
            [new SpreadsheetImportTransformRuleConfig("Trim")]);
    }

    public static SpreadsheetImportColumnPattern Date(string sourceColumnName, string targetFieldName, bool isRequired, params string[] acceptedHeaders)
    {
        return new SpreadsheetImportColumnPattern(
            sourceColumnName,
            targetFieldName,
            isRequired,
            acceptedHeaders,
            null,
            [
                new SpreadsheetImportTransformRuleConfig("Trim"),
                new SpreadsheetImportTransformRuleConfig("BrazilianDate", """{"formats":["yyyyMMdd","dd/MM/yyyy","yyyy-MM-dd","MM/dd/yyyy"]}""")
            ]);
    }
}
