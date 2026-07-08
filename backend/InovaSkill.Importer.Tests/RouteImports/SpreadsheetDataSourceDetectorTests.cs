using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class SpreadsheetDataSourceDetectorTests
{
    [Fact]
    public void Detect_RecognizesCustomersFromHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clientes");
        WriteHeaders(sheet, ["Codigo", "Loja", "CNPJ/CPF", "Nome", "N Fantasia", "Tipo", "Estado", "Municipio"]);

        Assert.Equal(CustomerImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RecognizesFiscalMovementsFromHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Dados");
        WriteHeaders(sheet, ["Documento", "Data", "ITEM", "CLIENTE", "LOJA", "PRODUTO", "QUANTIDADE", "Peso Bruto"]);

        Assert.Equal(FiscalImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RecognizesRoutesFromWeekdayAndRouteMarker()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Segunda-feira");
        sheet.Cell(3, 2).Value = "ROTA 01";
        sheet.Cell(3, 3).Value = "Cidades da Rota";

        Assert.Equal(RouteImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RecognizesProductsFromHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Listagem do Browse");
        sheet.Cell(2, 1).Value = "Codigo";
        sheet.Cell(2, 2).Value = "*Cod.OnClick";
        sheet.Cell(2, 3).Value = "Descricao";
        sheet.Cell(2, 4).Value = "Tipo";
        sheet.Cell(2, 5).Value = "Unidade";
        sheet.Cell(2, 6).Value = "Grupo";

        Assert.Equal(ProductImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RecognizesInventoryCurrentFromHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("1-Saldos em Estoque");
        sheet.Cell(2, 1).Value = "CODIGO";
        sheet.Cell(2, 2).Value = "FL";
        sheet.Cell(2, 3).Value = "ARMZ";
        sheet.Cell(2, 4).Value = "SALDO EM ESTOQUE";
        sheet.Cell(2, 5).Value = "EMPENHO PARA REQ/PV/RESERVA";
        sheet.Cell(2, 6).Value = "ESTOQUE DISPONIVEL";
        sheet.Cell(2, 7).Value = "VALOR EM ESTOQUE";
        sheet.Cell(2, 8).Value = "VALOR EMPENHADO";

        Assert.Equal(InventoryCurrentImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RecognizesDailyInventoryFromHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("05.2026");
        sheet.Cell(1, 1).Value = "CÓD.";
        sheet.Cell(1, 2).Value = "CÓD";
        sheet.Cell(1, 3).Value = "PRODUTO";
        sheet.Cell(1, 5).Value = "ATUAL";
        sheet.Cell(1, 6).Value = new DateTime(2026, 5, 1);
        sheet.Cell(2, 3).Value = "MOVIMENTAÇÕES";
        sheet.Cell(2, 6).Value = "ENTRADA";
        sheet.Cell(2, 7).Value = "SAIDA";
        sheet.Cell(2, 8).Value = "ATUAL";

        Assert.Equal(DailyInventoryImportCodes.DataSource, Detect(workbook));
    }

    [Fact]
    public void Detect_RejectsUnknownWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Dados").Cell(1, 1).Value = "Cabeçalho desconhecido";

        var exception = Assert.Throws<StructuralImportException>(() => Detect(workbook));

        Assert.Contains("Não foi possível identificar", exception.Message);
    }

    [Fact]
    public void UploadPolicy_IsOneHundredMegabytes()
    {
        Assert.Equal(100L * 1024 * 1024, RouteImportCodes.MaximumUploadSizeBytes);
    }

    private static string Detect(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new SpreadsheetDataSourceDetector().Detect(stream);
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            sheet.Cell(2, index + 1).Value = headers[index];
    }
}
