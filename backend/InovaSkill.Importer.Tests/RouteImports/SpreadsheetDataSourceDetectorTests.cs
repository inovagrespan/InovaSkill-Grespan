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
