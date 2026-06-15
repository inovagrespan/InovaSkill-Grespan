using ClosedXML.Excel;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using System.Globalization;
using System.Text;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed class RoutePlanningWorkbookParser : IRoutePlanningWorkbookParser
{
    private const int RouteNameColumn = 2;
    private const int DestinationColumn = 3;
    private const int DeliveriesColumn = 4;
    private const int AverageLoadColumn = 5;
    private const int MaxRelevantColumn = 5;
    private const string CapacitySheetName = "Capacidade dos Caminhoes";

    public RoutePlanningWorkbookParseResult Parse(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var capacitySheet = workbook.Worksheets.FirstOrDefault(sheet =>
            NormalizeToken(sheet.Name) == NormalizeToken(CapacitySheetName));

        var capacities = capacitySheet is null
            ? []
            : ParseCapacities(capacitySheet);

        var capacitiesByVehicle = capacities
            .GroupBy(x => NormalizeVehicleType(x.VehicleType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CapacityKg, StringComparer.OrdinalIgnoreCase);

        var routes = new List<RoutePlan>();

        foreach (var sheet in workbook.Worksheets.Where(sheet =>
                     NormalizeToken(sheet.Name) != NormalizeToken(CapacitySheetName)))
        {
            routes.AddRange(ParseRouteSheet(sheet, capacitiesByVehicle));
        }

        return new RoutePlanningWorkbookParseResult(capacities, routes);
    }

    private static IReadOnlyList<TruckCapacityProfile> ParseCapacities(IXLWorksheet sheet)
    {
        var result = new List<TruckCapacityProfile>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var vehicleType = NormalizeText(sheet.Cell(rowNumber, 2).GetString());
            if (string.IsNullOrWhiteSpace(vehicleType) ||
                NormalizeToken(vehicleType) == "caminhao")
            {
                continue;
            }

            if (!TryParseDecimal(sheet.Cell(rowNumber, 3).Value, out var capacityKg))
            {
                continue;
            }

            result.Add(new TruckCapacityProfile
            {
                VehicleType = CanonicalizeVehicleType(vehicleType),
                CapacityKg = capacityKg
            });
        }

        return result;
    }

    private static IReadOnlyList<RoutePlan> ParseRouteSheet(
        IXLWorksheet sheet,
        IReadOnlyDictionary<string, decimal> capacitiesByVehicle)
    {
        var result = new List<RoutePlan>();
        var weekdayLabel = ResolveWeekdayLabel(sheet);
        var weekdayOrder = ResolveWeekdayOrder(weekdayLabel);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        RoutePlanDraft? currentRoute = null;
        var routeOrder = 0;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var routeOrNote = NormalizeText(sheet.Cell(rowNumber, RouteNameColumn).GetString());
            var destination = NormalizeText(sheet.Cell(rowNumber, DestinationColumn).GetString());
            var deliveriesRaw = NormalizeText(GetCellText(sheet.Cell(rowNumber, DeliveriesColumn)));
            var averageLoadRaw = NormalizeText(GetCellText(sheet.Cell(rowNumber, AverageLoadColumn)));

            if (IsRouteHeader(routeOrNote, destination))
            {
                if (currentRoute is not null)
                {
                    result.Add(BuildRoute(currentRoute, capacitiesByVehicle));
                }

                routeOrder++;
                currentRoute = new RoutePlanDraft(
                    sheet.Name,
                    weekdayLabel,
                    weekdayOrder,
                    routeOrder,
                    routeOrNote);
                continue;
            }

            if (currentRoute is null)
            {
                continue;
            }

            if (IsVehicleSummaryRow(routeOrNote, destination))
            {
                currentRoute.VehicleType = CanonicalizeVehicleType(routeOrNote);
                currentRoute.TotalDeliveries = TryParseInteger(sheet.Cell(rowNumber, DeliveriesColumn).Value, out var totalDeliveries)
                    ? totalDeliveries
                    : null;
                currentRoute.TotalAverageLoadKg = TryParseDecimal(sheet.Cell(rowNumber, AverageLoadColumn).Value, out var totalAverageLoadKg)
                    ? totalAverageLoadKg
                    : null;
                result.Add(BuildRoute(currentRoute, capacitiesByVehicle));
                currentRoute = null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(destination))
            {
                if (TryParseDecimal(sheet.Cell(rowNumber, AverageLoadColumn).Value, out var pendingTotalLoad))
                {
                    currentRoute.TotalAverageLoadKg = pendingTotalLoad;
                }

                if (TryParseInteger(sheet.Cell(rowNumber, DeliveriesColumn).Value, out var pendingTotalDeliveries))
                {
                    currentRoute.TotalDeliveries = pendingTotalDeliveries;
                }

                continue;
            }

            var stopOrder = currentRoute.Stops.Count + 1;
            currentRoute.Stops.Add(new RouteStop
            {
                StopOrder = stopOrder,
                DestinationName = destination,
                DeliveriesRaw = deliveriesRaw,
                DeliveriesCount = TryParseInteger(sheet.Cell(rowNumber, DeliveriesColumn).Value, out var deliveriesCount)
                    ? deliveriesCount
                    : null,
                AverageLoadKg = TryParseDecimal(sheet.Cell(rowNumber, AverageLoadColumn).Value, out var averageLoadKg)
                    ? averageLoadKg
                    : null,
                Note = IsStopNote(routeOrNote) ? routeOrNote : string.Empty
            });
        }

        if (currentRoute is not null)
        {
            result.Add(BuildRoute(currentRoute, capacitiesByVehicle));
        }

        return result;
    }

    private static RoutePlan BuildRoute(
        RoutePlanDraft draft,
        IReadOnlyDictionary<string, decimal> capacitiesByVehicle)
    {
        var capacityKg = capacitiesByVehicle.TryGetValue(NormalizeVehicleType(draft.VehicleType), out var resolvedCapacity)
            ? resolvedCapacity
            : (decimal?)null;

        var totalAverageLoadKg = draft.TotalAverageLoadKg
            ?? (draft.Stops.Any(stop => stop.AverageLoadKg.HasValue)
                ? draft.Stops.Sum(stop => stop.AverageLoadKg ?? 0m)
                : (decimal?)null);
        var totalDeliveries = draft.TotalDeliveries
            ?? (draft.Stops.Any(stop => stop.DeliveriesCount.HasValue)
                ? draft.Stops.Sum(stop => stop.DeliveriesCount ?? 0)
                : (int?)null);
        var occupancyPercent = capacityKg is > 0m && totalAverageLoadKg is > 0m
            ? Math.Round((totalAverageLoadKg.Value / capacityKg.Value) * 100m, 2)
            : (decimal?)null;

        return new RoutePlan
        {
            SheetName = draft.SheetName,
            WeekdayLabel = draft.WeekdayLabel,
            WeekdayOrder = draft.WeekdayOrder,
            RouteOrder = draft.RouteOrder,
            RouteName = draft.RouteName,
            VehicleType = draft.VehicleType,
            VehicleCapacityKg = capacityKg,
            TotalDeliveries = totalDeliveries,
            TotalAverageLoadKg = totalAverageLoadKg,
            OccupancyPercent = occupancyPercent,
            Stops = draft.Stops
        };
    }

    private static bool IsRouteHeader(string routeOrNote, string destination)
    {
        return !string.IsNullOrWhiteSpace(routeOrNote)
            && NormalizeToken(destination) == NormalizeToken("CIDADES DA ROTA");
    }

    private static bool IsVehicleSummaryRow(string routeOrNote, string destination)
    {
        return string.IsNullOrWhiteSpace(destination) && !string.IsNullOrWhiteSpace(routeOrNote)
            && NormalizeVehicleType(routeOrNote) is "truck" or "toco" or "acelo";
    }

    private static bool IsStopNote(string routeOrNote)
    {
        if (string.IsNullOrWhiteSpace(routeOrNote))
        {
            return false;
        }

        return NormalizeVehicleType(routeOrNote) is not "truck" and not "toco" and not "acelo";
    }

    private static string ResolveWeekdayLabel(IXLWorksheet sheet)
    {
        for (var rowNumber = 1; rowNumber <= Math.Min(6, sheet.LastRowUsed()?.RowNumber() ?? 0); rowNumber++)
        {
            var candidate = NormalizeText(sheet.Cell(rowNumber, RouteNameColumn).GetString());
            if (!string.IsNullOrWhiteSpace(candidate) && NormalizeToken(candidate) != NormalizeToken("ROTAS POR CIDADE E DIA DA SEMANA"))
            {
                return candidate;
            }
        }

        return sheet.Name;
    }

    private static int ResolveWeekdayOrder(string weekdayLabel)
    {
        return NormalizeToken(weekdayLabel) switch
        {
            "segunda" => 1,
            "terca" => 2,
            "quarta" => 3,
            "quinta" => 4,
            "sexta" => 5,
            _ => 0
        };
    }

    private static string CanonicalizeVehicleType(string rawVehicleType)
    {
        return NormalizeVehicleType(rawVehicleType) switch
        {
            "truck" => "Truck",
            "toco" => "Toco",
            "acelo" => "Acelo",
            _ => NormalizeText(rawVehicleType)
        };
    }

    private static string NormalizeVehicleType(string value)
    {
        var token = NormalizeToken(value);
        if (token.Contains("truck", StringComparison.OrdinalIgnoreCase))
        {
            return "truck";
        }

        if (token.Contains("toco", StringComparison.OrdinalIgnoreCase))
        {
            return "toco";
        }

        if (token.Contains("acelo", StringComparison.OrdinalIgnoreCase) ||
            token.Contains("acello", StringComparison.OrdinalIgnoreCase))
        {
            return "acelo";
        }

        return token;
    }

    private static string GetCellText(IXLCell cell)
    {
        return cell.Value.Type switch
        {
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => cell.GetString()
        };
    }

    private static bool TryParseInteger(XLCellValue value, out int parsed)
    {
        if (value.Type == XLDataType.Number)
        {
            parsed = Convert.ToInt32(value.GetNumber());
            return true;
        }

        var text = NormalizeText(value.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseDecimal(XLCellValue value, out decimal parsed)
    {
        if (value.Type == XLDataType.Number)
        {
            parsed = Convert.ToDecimal(value.GetNumber(), CultureInfo.InvariantCulture);
            return true;
        }

        var text = NormalizeText(value.ToString(CultureInfo.InvariantCulture));
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string NormalizeToken(string value)
    {
        var normalized = NormalizeText(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private sealed record RoutePlanDraft(
        string SheetName,
        string WeekdayLabel,
        int WeekdayOrder,
        int RouteOrder,
        string RouteName)
    {
        public string VehicleType { get; set; } = string.Empty;
        public int? TotalDeliveries { get; set; }
        public decimal? TotalAverageLoadKg { get; set; }
        public List<RouteStop> Stops { get; } = [];
    }
}
