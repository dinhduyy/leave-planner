using LeavePlanner.Application.Models;
using LeavePlanner.Application.Services;

namespace LeavePlanner.Application.Tests;

public sealed class HolidayCalendarNormalizerTests
{
    [Fact]
    public void KeepsOnlyApplicablePublicHolidaysAndCombinesNamesPerDay()
    {
        var date = new DateOnly(2026, 5, 1);
        var records = new[]
        {
            Holiday(date, "Regional", false, ["AT-9"], ["Public"]),
            Holiday(date, "National", true, null, ["Public", "Bank"]),
            Holiday(date, "Regional", false, ["AT-9"], ["Public"]),
            Holiday(date, "Other region", false, ["AT-1"], ["Public"]),
            Holiday(date, "Bank only", true, null, ["Bank"]),
            Holiday(date.AddDays(1), "Out of range", true, null, ["Public"])
        };

        var holidays = HolidayCalendarNormalizer.Normalize(records, "AT", "AT-9", date, date);

        var only = Assert.Single(holidays);
        Assert.Equal(date, only.Date);
        Assert.Equal(new[] { "National", "Regional" }, only.Names);
    }

    [Fact]
    public void MissingSubdivisionUsesNationalOnly()
    {
        var date = new DateOnly(2026, 5, 1);
        var records = new[]
        {
            Holiday(date, "Regional", false, ["AT-9"], ["Public"]),
            Holiday(date.AddDays(1), "National", true, null, ["Public"])
        };

        var holidays = HolidayCalendarNormalizer.Normalize(records, "AT", null, date, date.AddDays(1));

        Assert.Equal("National", Assert.Single(holidays).Names.Single());
    }

    [Fact]
    public void UnorderedRecordsProduceStableDateAndNameOrder()
    {
        var date = new DateOnly(2026, 5, 1);
        var records = new[]
        {
            Holiday(date.AddDays(1), "Zulu", true, null, ["Public"]),
            Holiday(date, "Beta", true, null, ["Public"]),
            Holiday(date, "Alpha", true, null, ["Public"])
        };

        var holidays = HolidayCalendarNormalizer.Normalize(records, "AT", null, date, date.AddDays(1));

        Assert.Equal(new[] { date, date.AddDays(1) }, holidays.Select(holiday => holiday.Date));
        Assert.Equal(new[] { "Alpha", "Beta" }, holidays.First().Names);
    }

    [Fact]
    public void PublicTypeMatchingIsConsistentWithProviderParsing()
    {
        var date = new DateOnly(2026, 5, 1);
        var records = new[] { Holiday(date, "Public", true, null, ["public"]) };

        var holidays = HolidayCalendarNormalizer.Normalize(records, "AT", null, date, date);

        Assert.Single(holidays);
    }

    private static HolidayRecord Holiday(DateOnly date, string name, bool national,
        IReadOnlyList<string>? regions, IReadOnlyList<string> types) =>
        new(date, name, "AT", national, regions, types);
}
