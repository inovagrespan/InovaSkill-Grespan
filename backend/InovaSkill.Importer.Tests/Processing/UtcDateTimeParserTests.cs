using InovaSkill.Importer.Infrastructure.Processing.Buffers;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class UtcDateTimeParserTests
{
    public static TheoryData<string, DateTime> ValidDates()
    {
        return new TheoryData<string, DateTime>
        {
            { "2025-07-31", Utc(2025, 7, 31, 0, 0, 0) },
            { "2025-07-31 13:45:59", Utc(2025, 7, 31, 13, 45, 59) },
            { "2025-07-31T13:45:59", Utc(2025, 7, 31, 13, 45, 59) },
            { "20250731", Utc(2025, 7, 31, 0, 0, 0) },
            { "31/07/2025", Utc(2025, 7, 31, 0, 0, 0) },
            { "31/07/2025 00:00:00", Utc(2025, 7, 31, 0, 0, 0) },
            { "31/07/2025 7:05:09", Utc(2025, 7, 31, 7, 5, 9) },
            { "1/7/2025", Utc(2025, 7, 1, 0, 0, 0) },
            { "1/7/2025 7:05", Utc(2025, 7, 1, 7, 5, 0) },
            { "07/31/2025", Utc(2025, 7, 31, 0, 0, 0) },
            { "07/31/2025 23:59:58", Utc(2025, 7, 31, 23, 59, 58) },
            { " 31/07/2025 00:00:00 ", Utc(2025, 7, 31, 0, 0, 0) }
        };
    }

    [Theory]
    [MemberData(nameof(ValidDates))]
    public void ParseRequired_ShouldAcceptKnownImportDateFormats(string value, DateTime expected)
    {
        var result = UtcDateTimeParser.ParseRequired(value);

        Assert.Equal(expected, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ParseRequired_ShouldPreferBrazilianOrderForAmbiguousSlashDates()
    {
        var result = UtcDateTimeParser.ParseRequired("01/02/2025");

        Assert.Equal(Utc(2025, 2, 1, 0, 0, 0), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("31/13/2025")]
    [InlineData("2025-02-30")]
    [InlineData("não é data")]
    public void TryParse_ShouldRejectInvalidDates(string value)
    {
        Assert.False(UtcDateTimeParser.TryParse(value, out _));
    }

    [Fact]
    public void ParseOrDefaultUtcNow_ShouldReturnUtcNowForInvalidOptionalDate()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = UtcDateTimeParser.ParseOrDefaultUtcNow("sem data");
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(result, before, after);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
