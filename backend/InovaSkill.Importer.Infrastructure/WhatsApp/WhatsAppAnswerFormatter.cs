namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public static class WhatsAppAnswerFormatter
{
    private const string RouteMarker = "[ROTA]";
    private const string DataPeriodLabel = "Período dos dados:";

    public static string Format(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return answer;

        var output = new List<string>();
        var routeNumber = 0;

        foreach (var rawLine in answer.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(DataPeriodLabel, StringComparison.OrdinalIgnoreCase))
            {
                var period = line[DataPeriodLabel.Length..].Trim();
                output.Add($"📅 *{DataPeriodLabel}* {period}".TrimEnd());
                continue;
            }

            if (!line.StartsWith(RouteMarker, StringComparison.OrdinalIgnoreCase))
            {
                output.Add(rawLine.TrimEnd());
                continue;
            }

            routeNumber++;
            AppendRoute(output, line[RouteMarker.Length..].Trim(), routeNumber);
        }

        return string.Join('\n', CollapseBlankLines(output)).Trim();
    }

    private static void AppendRoute(List<string> output, string routeContract, int routeNumber)
    {
        var parts = routeContract.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var routeName = parts.FirstOrDefault() ?? "Rota não informada";

        if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[^1])) output.Add(string.Empty);
        output.Add($"🚨 *{routeNumber}. {routeName}*");

        foreach (var part in parts.Skip(1))
        {
            var separatorIndex = part.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
            {
                output.Add($"• {part}");
                continue;
            }

            var label = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();
            output.Add($"• {label}: {value}");
        }

        output.Add(string.Empty);
    }

    private static IEnumerable<string> CollapseBlankLines(IEnumerable<string> lines)
    {
        var previousWasBlank = false;
        foreach (var line in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && previousWasBlank) continue;

            yield return line;
            previousWasBlank = isBlank;
        }
    }
}
