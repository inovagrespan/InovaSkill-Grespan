namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteSnapshotDatePolicy
{
    private const string BusinessTimeZoneId = "America/Sao_Paulo";
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);

    public static DateTime GetExclusiveUtcEnd(DateOnly referenceDate)
    {
        var localEnd = DateTime.SpecifyKind(
            referenceDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(localEnd, BusinessTimeZone);
    }
}
