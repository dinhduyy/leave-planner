using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Models;
using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Planning;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Application.Tests;

public sealed class LeavePlanningServiceTests
{
    private static readonly RecommendationStrategyRegistry Strategies = new([
        new LongestBreakStrategy(), new MinimumLeaveStrategy(), new BestEfficiencyStrategy()
    ]);

    [Fact]
    public async Task RejectsUnsupportedCountryBeforeFetchingHolidays()
    {
        var source = new StubHolidaySource();
        var service = CreateService(source, new StubSubdivisions());

        var error = await Assert.ThrowsAsync<InvalidPlanningRequestException>(() =>
            service.RecommendAsync(Query("ZZ", null), CancellationToken.None));

        Assert.Equal("countryCode", error.Field);
        Assert.Empty(source.RequestedYears);
    }

    [Fact]
    public async Task RejectsUnknownSubdivisionWithoutTreatingItAsNationalOnly()
    {
        var source = new StubHolidaySource();
        var service = CreateService(source, new StubSubdivisions("AT-9"));

        var error = await Assert.ThrowsAsync<InvalidPlanningRequestException>(() =>
            service.RecommendAsync(Query("AT", "AT-0"), CancellationToken.None));

        Assert.Equal("subdivisionCode", error.Field);
        Assert.Empty(source.RequestedYears);
    }

    [Fact]
    public async Task OneFailedYearPreventsPartialRecommendation()
    {
        var source = new StubHolidaySource
        {
            Fetch = (_, year) => year == 2027
                ? throw new HolidaySourceException(HolidaySourceFailure.Unavailable, "Unavailable")
                : []
        };
        var service = CreateService(source, new StubSubdivisions());
        var query = new PlanningQuery("AT", null, new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1),
            2, PlanningPriority.LongestBreak, 1);

        await Assert.ThrowsAsync<HolidaySourceException>(() => service.RecommendAsync(query, CancellationToken.None));
        Assert.Equal(new[] { 2026, 2027 }, source.RequestedYears);
    }

    [Fact]
    public async Task EmptyValidCalendarCanReturnNoRecommendation()
    {
        var source = new StubHolidaySource();
        var service = CreateService(source, new StubSubdivisions());

        var result = await service.RecommendAsync(Query("AT", null), CancellationToken.None);

        Assert.Null(result.Recommendation);
        Assert.Contains("budget", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NationalOnlyDoesNotIncludeRegionalHoliday()
    {
        var date = new DateOnly(2026, 5, 1);
        var source = new StubHolidaySource
        {
            Fetch = (_, _) => [
                new HolidayRecord(date, "Regional", "AT", false, ["AT-9"], ["Public"]),
                new HolidayRecord(date, "National", "AT", true, null, ["Public"])
            ]
        };
        var service = CreateService(source, new StubSubdivisions("AT-9"));
        var query = new PlanningQuery("AT", null, date, date, 0, PlanningPriority.LongestBreak, 1);

        var result = await service.RecommendAsync(query, CancellationToken.None);

        Assert.NotNull(result.Recommendation);
        Assert.Equal("National", Assert.Single(result.HolidaysInRecommendation).Names.Single());
    }

    private static LeavePlanningService CreateService(StubHolidaySource holidays,
        StubSubdivisions subdivisions) => new(holidays, subdivisions, new LeavePlannerOptimizer(), Strategies);

    private static PlanningQuery Query(string country, string? subdivision) => new(country, subdivision,
        new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1), 0, PlanningPriority.LongestBreak, 1);

    private sealed class StubHolidaySource : IHolidaySource
    {
        public Func<string, int, IReadOnlyList<HolidayRecord>> Fetch { get; init; } = (_, _) => [];
        public List<int> RequestedYears { get; } = [];

        public Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["AT"], StringComparer.Ordinal));

        public Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(string countryCode, int year,
            CancellationToken cancellationToken)
        {
            RequestedYears.Add(year);
            return Task.FromResult(Fetch(countryCode, year));
        }
    }

    private sealed class StubSubdivisions(params string[] subdivisions) : ISubdivisionCatalog
    {
        public bool Contains(string countryCode, string subdivisionCode) =>
            subdivisions.Contains(subdivisionCode, StringComparer.Ordinal);
    }
}
