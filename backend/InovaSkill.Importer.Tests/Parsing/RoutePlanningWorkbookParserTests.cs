using ClosedXML.Excel;
using InovaSkill.Importer.Infrastructure.Parsing;

namespace InovaSkill.Importer.Tests.Parsing;

public class RoutePlanningWorkbookParserTests
{
    [Fact]
    public void Parse_ReadsRoutesStopsAndTruckOccupancy()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"route-planning-{Guid.NewGuid():N}.xlsx");
        CreateWorkbook(filePath);

        try
        {
            var parser = new RoutePlanningWorkbookParser();

            var result = parser.Parse(filePath);

            Assert.Equal(3, result.TruckCapacities.Count);

            var mondayRoute = Assert.Single(result.Routes, route => route.WeekdayOrder == 1);
            Assert.Equal("RIO PRETO", mondayRoute.RouteName);
            Assert.Equal("Truck", mondayRoute.VehicleType);
            Assert.Equal(10300m, mondayRoute.VehicleCapacityKg);
            Assert.Equal(12, mondayRoute.TotalDeliveries);
            Assert.Equal(11033.0267m, Math.Round(mondayRoute.TotalAverageLoadKg ?? 0m, 4));
            Assert.Equal(107.12m, mondayRoute.OccupancyPercent);
            Assert.Equal(4, mondayRoute.Stops.Count);
            Assert.Equal("SAO JOSE DO RIO PRETO", mondayRoute.Stops.First().DestinationName);

            var tuesdayRoute = Assert.Single(result.Routes, route => route.WeekdayOrder == 2);
            Assert.Equal("TAUSTES", tuesdayRoute.RouteName);
            Assert.Equal("Acelo", tuesdayRoute.VehicleType);
            Assert.Equal(3300m, tuesdayRoute.VehicleCapacityKg);
            Assert.Equal(0m, tuesdayRoute.TotalAverageLoadKg);
            Assert.Contains(tuesdayRoute.Stops, stop => stop.Note.Contains("DESCARGA", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Parse_KeepsRouteWhenSummaryHasNoVehicleType()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"route-planning-no-vehicle-{Guid.NewGuid():N}.xlsx");
        CreateWorkbookWithMissingVehicleSummary(filePath);

        try
        {
            var parser = new RoutePlanningWorkbookParser();

            var result = parser.Parse(filePath);

            var route = Assert.Single(result.Routes);
            Assert.Equal("MONTE ALTO", route.RouteName);
            Assert.True(string.IsNullOrWhiteSpace(route.VehicleType));
            Assert.Equal(3958.828m, route.TotalAverageLoadKg);
            Assert.Null(route.VehicleCapacityKg);
            Assert.Null(route.OccupancyPercent);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static void CreateWorkbook(string filePath)
    {
        using var workbook = new XLWorkbook();

        var monday = workbook.AddWorksheet("SEGUNDA NOVA ");
        monday.Cell(1, 2).Value = "ROTAS POR CIDADE E DIA DA SEMANA ";
        monday.Cell(3, 2).Value = "SEGUNDA ";
        monday.Cell(4, 2).Value = "RIO PRETO";
        monday.Cell(4, 3).Value = "CIDADES DA ROTA ";
        monday.Cell(4, 4).Value = "Entregas ";
        monday.Cell(4, 5).Value = "Media/Dia ";
        monday.Cell(5, 3).Value = "SAO JOSE DO RIO PRETO";
        monday.Cell(5, 4).Value = 4;
        monday.Cell(5, 5).Value = 6762.7740;
        monday.Cell(6, 3).Value = "BADY BASSITT";
        monday.Cell(6, 4).Value = 4;
        monday.Cell(6, 5).Value = 1491.6400;
        monday.Cell(7, 3).Value = "JOSE BONIFACIO";
        monday.Cell(7, 4).Value = 2;
        monday.Cell(7, 5).Value = 1638.9620;
        monday.Cell(8, 3).Value = "NOVA ALIANCA";
        monday.Cell(8, 4).Value = 2;
        monday.Cell(8, 5).Value = 1139.6507;
        monday.Cell(9, 2).Value = "Truck ";
        monday.Cell(9, 4).Value = 12;
        monday.Cell(9, 5).Value = 11033.0267;

        var tuesday = workbook.AddWorksheet("TERCA NOVA");
        tuesday.Cell(1, 2).Value = "ROTAS POR CIDADE E DIA DA SEMANA ";
        tuesday.Cell(3, 2).Value = "TERCA";
        tuesday.Cell(4, 2).Value = "TAUSTES";
        tuesday.Cell(4, 3).Value = "CIDADES DA ROTA ";
        tuesday.Cell(36, 2).Value = "DESCARGA, CALCULAR 0,046";
        tuesday.Cell(36, 3).Value = "(COLINAS) (SAO JOSE DOS CAMPOS)";
        tuesday.Cell(36, 5).Value = 0;
        tuesday.Cell(37, 2).Value = "DESCARGA, CALCULAR 0,046";
        tuesday.Cell(37, 3).Value = "TAUBATE";
        tuesday.Cell(37, 5).Value = 0;
        tuesday.Cell(38, 2).Value = "Acello";
        tuesday.Cell(38, 5).Value = 0;

        var capacity = workbook.AddWorksheet("Capacidade dos Caminhoes");
        capacity.Cell(1, 2).Value = "CAPACIADE DOS CAMINHOES ";
        capacity.Cell(3, 2).Value = "Caminhao ";
        capacity.Cell(3, 3).Value = "Capacidade - KG ";
        capacity.Cell(4, 2).Value = "Truck ";
        capacity.Cell(4, 3).Value = 10300;
        capacity.Cell(5, 2).Value = "Toco ";
        capacity.Cell(5, 3).Value = 7700;
        capacity.Cell(6, 2).Value = "Acelo ";
        capacity.Cell(6, 3).Value = 3300;

        workbook.SaveAs(filePath);
    }

    private static void CreateWorkbookWithMissingVehicleSummary(string filePath)
    {
        using var workbook = new XLWorkbook();

        var tuesday = workbook.AddWorksheet("TERCA NOVA");
        tuesday.Cell(1, 2).Value = "ROTAS POR CIDADE E DIA DA SEMANA ";
        tuesday.Cell(3, 2).Value = "TERCA";
        tuesday.Cell(10, 2).Value = "MONTE ALTO";
        tuesday.Cell(10, 3).Value = "CIDADES DA ROTA ";
        tuesday.Cell(11, 3).Value = "JABOTICABAL";
        tuesday.Cell(11, 4).Value = 2;
        tuesday.Cell(11, 5).Value = 3835.0767;
        tuesday.Cell(12, 3).Value = "MONTE ALTO";
        tuesday.Cell(12, 4).Value = 4;
        tuesday.Cell(12, 5).Value = 123.7513;
        tuesday.Cell(13, 5).Value = 3958.8280;

        workbook.SaveAs(filePath);
    }
}
