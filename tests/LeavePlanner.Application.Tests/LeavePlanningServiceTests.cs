using LeavePlanner.Application.Enums;
using LeavePlanner.Domain.Enums;
using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Common;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Models;
using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Models;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Application.Tests;

public sealed class LeavePlanningServiceTests
{
    private static readonly IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy> Strategies = new IRecommendationStrategy[]
    {
        new LongestBreakStrategy(), new MinimumLeaveStrategy(), new BestEfficiencyStrategy()
    }.ToDictionary(strategy => strategy.Priority);

    [Fact]
    public async Task RejectsUnregisteredPriority()
    {
        var service = CreateService(new StubHolidaySource(), new StubSubdivisions());
        var query = Query("AT", null) with { Priority = (PlanningPriority)999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.RecommendAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsUnsupportedCountryBeforeFetchingHolidays()
    {
        var source = new StubHolidaySource();
        var service = CreateService(source, new StubSubdivisions());

        var outcome = await service.RecommendAsync(Query("ZZ", null), CancellationToken.None);

        var failure = Assert.IsType<Result<PlanningResult, PlanningError>.Failure>(outcome);
        Assert.Equal("unsupported_country", failure.Error.Code);
        Assert.Equal(nameof(PlanningQuery.CountryCode), failure.Error.Field);
        Assert.Empty(source.RequestedYears);
    }

    [Fact]
    public async Task RejectsUnknownSubdivisionWithoutTreatingItAsNationalOnly()
    {
        var source = new StubHolidaySource();
        var service = CreateService(source, new StubSubdivisions("AT-9"));

        var outcome = await service.RecommendAsync(Query("AT", "AT-0"), CancellationToken.None);

        var failure = Assert.IsType<Result<PlanningResult, PlanningError>.Failure>(outcome);
        Assert.Equal("invalid_subdivision", failure.Error.Code);
        Assert.Equal(nameof(PlanningQuery.SubdivisionCode), failure.Error.Field);
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

        var outcome = await service.RecommendAsync(Query("AT", null), CancellationToken.None);
        var result = Assert.IsType<Result<PlanningResult, PlanningError>.Success>(outcome).Value;

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

        var outcome = await service.RecommendAsync(query, CancellationToken.None);
        var result = Assert.IsType<Result<PlanningResult, PlanningError>.Success>(outcome).Value;

        Assert.NotNull(result.Recommendation);
        Assert.Equal("National", Assert.Single(result.HolidaysInRecommendation).Names.Single());
    }

    [Fact]
    public async Task ConnectsHolidayAndWeekendWithoutChargingLeave()
    {
        var from = new DateOnly(2026, 1, 2);
        var selection = await Recommend(from, new DateOnly(2026, 1, 4), 0, 1,
            PlanningPriority.LongestBreak, [Holiday(from)]);

        Assert.NotNull(selection);
        Assert.Equal(3, selection.Candidate.TotalDays);
        Assert.Equal(0, selection.Candidate.LeaveDaysRequired);
        Assert.Empty(selection.LeaveDates);
    }

    [Fact]
    public async Task RespectsBudgetAndReturnsSortedLeaveDates()
    {
        var selection = await Recommend(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11), 2, 1,
            PlanningPriority.LongestBreak, [Holiday(new DateOnly(2026, 1, 7))]);

        Assert.NotNull(selection);
        Assert.Equal(new DateOnly(2026, 1, 7), selection.Candidate.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 11), selection.Candidate.EndDate);
        Assert.Equal(5, selection.Candidate.TotalDays);
        Assert.Equal(2, selection.Candidate.LeaveDaysRequired);
        Assert.Equal([new DateOnly(2026, 1, 8), new DateOnly(2026, 1, 9)], selection.LeaveDates);
    }

    [Fact]
    public async Task MinimumLeaveCanChooseShorterFreeBreak()
    {
        var selection = await Recommend(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11), 5, 2,
            PlanningPriority.MinimumLeave);

        Assert.NotNull(selection);
        Assert.Equal(new DateOnly(2026, 1, 10), selection.Candidate.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 11), selection.Candidate.EndDate);
        Assert.Equal(0, selection.Candidate.LeaveDaysRequired);
    }

    [Fact]
    public async Task IncludesLeapDayInAnInclusiveYearRange()
    {
        var selection = await Recommend(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31),
            int.MaxValue, 366, PlanningPriority.LongestBreak);

        Assert.NotNull(selection);
        Assert.Equal(366, selection.Candidate.TotalDays);
        Assert.Contains(new DateOnly(2024, 2, 29), selection.LeaveDates);
        Assert.Equal(selection.Candidate.LeaveDaysRequired, selection.LeaveDates.Count);
    }

    [Fact]
    public async Task HandlesDateOnlyLimits()
    {
        var minimum = await Recommend(DateOnly.MinValue, DateOnly.MinValue, 1, 1,
            PlanningPriority.LongestBreak);
        var maximum = await Recommend(DateOnly.MaxValue, DateOnly.MaxValue, 1, 1,
            PlanningPriority.LongestBreak);

        Assert.Equal(DateOnly.MinValue, minimum?.Candidate.StartDate);
        Assert.Equal([DateOnly.MinValue], minimum?.LeaveDates);
        Assert.Equal(DateOnly.MaxValue, maximum?.Candidate.EndDate);
        Assert.Equal([DateOnly.MaxValue], maximum?.LeaveDates);
    }

    [Fact]
    public async Task HolidayOrderAndDuplicatesDoNotChangeSelection()
    {
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 11);
        var first = Holiday(new DateOnly(2026, 1, 6));
        var second = Holiday(new DateOnly(2026, 1, 8));

        var original = await Recommend(from, to, 2, 1, PlanningPriority.LongestBreak, [first, second]);
        var reordered = await Recommend(from, to, 2, 1, PlanningPriority.LongestBreak, [second, first, first]);

        Assert.Equal(original?.Candidate, reordered?.Candidate);
        Assert.Equal(original?.LeaveDates, reordered?.LeaveDates);
    }

    [Fact]
    public async Task IncreasingBudgetDoesNotShortenLongestBreak()
    {
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 11);
        var smaller = await Recommend(from, to, 2, 1, PlanningPriority.LongestBreak);
        var larger = await Recommend(from, to, 3, 1, PlanningPriority.LongestBreak);

        Assert.NotNull(smaller);
        Assert.NotNull(larger);
        Assert.True(larger.Candidate.TotalDays >= smaller.Candidate.TotalDays);
    }

    private static HolidayRecord Holiday(DateOnly date) =>
        new(date, "Synthetic holiday", "AT", true, null, ["Public"]);

    private static async Task<LeavePlanSelection?> Recommend(DateOnly from, DateOnly to, int budget,
        int minTotalDays, PlanningPriority priority, IReadOnlyList<HolidayRecord>? holidays = null)
    {
        var source = new StubHolidaySource
        {
            Fetch = (_, year) => (holidays ?? []).Where(holiday => holiday.Date.Year == year).ToArray()
        };
        var service = CreateService(source, new StubSubdivisions());
        var query = new PlanningQuery("AT", null, from, to, budget, priority, minTotalDays);
        var outcome = await service.RecommendAsync(query, CancellationToken.None);
        return Assert.IsType<Result<PlanningResult, PlanningError>.Success>(outcome).Value.Recommendation;
    }

    private static LeavePlanningService CreateService(StubHolidaySource holidays,
        StubSubdivisions subdivisions) => new(holidays, subdivisions, Strategies);

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
