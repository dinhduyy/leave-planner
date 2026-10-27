using LeavePlanner.Domain.Calendar;
using LeavePlanner.Domain.Planning;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Domain.Tests;

public sealed class LeavePlannerOptimizerTests
{
    private readonly LeavePlannerOptimizer _optimizer = new();

    [Fact]
    public void FindBest_ConnectsHolidayAndWeekendWithoutChargingLeave()
    {
        // Synthetic calendar: Friday holiday followed by Saturday and Sunday.
        var result = _optimizer.FindBest(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 4), 0, 1,
            [new ApplicableHoliday(new DateOnly(2026, 1, 2), ["Synthetic Friday"])], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Equal(3, result.Candidate.TotalDays);
        Assert.Equal(0, result.Candidate.LeaveDaysRequired);
        Assert.Empty(result.LeaveDates);
    }

    [Fact]
    public void FindBest_RespectsBudgetAndReturnsSortedLeaveDates()
    {
        var result = _optimizer.FindBest(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11), 2, 1,
            [new ApplicableHoliday(new DateOnly(2026, 1, 7), ["Synthetic Wednesday"])], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 7), result.Candidate.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 11), result.Candidate.EndDate);
        Assert.Equal(5, result.Candidate.TotalDays);
        Assert.Equal(2, result.Candidate.LeaveDaysRequired);
        Assert.Equal([new DateOnly(2026, 1, 8), new DateOnly(2026, 1, 9)], result.LeaveDates);
    }

    [Fact]
    public void FindBest_ReturnsNullWhenMinimumLengthCannotBeReached()
    {
        var result = _optimizer.FindBest(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9), 0, 2,
            [], new LongestBreakStrategy());

        Assert.Null(result);
    }

    [Fact]
    public void FindBest_MinimumLeaveCanChooseShorterFreeBreak()
    {
        var result = _optimizer.FindBest(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11), 5, 2,
            [], new MinimumLeaveStrategy());

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 10), result.Candidate.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 11), result.Candidate.EndDate);
        Assert.Equal(0, result.Candidate.LeaveDaysRequired);
    }

    [Fact]
    public void FindBest_HandlesSingleDayAtDateOnlyMaximum()
    {
        var result = _optimizer.FindBest(DateOnly.MaxValue, DateOnly.MaxValue, 1, 1,
            [], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Equal(DateOnly.MaxValue, result.Candidate.EndDate);
        Assert.Single(result.LeaveDates);
    }

    [Fact]
    public void FindBest_DeduplicatesHolidaysAndIgnoresDatesOutsideTheRange()
    {
        var date = new DateOnly(2026, 1, 5);
        var result = _optimizer.FindBest(date, date, 0, 1,
            [new ApplicableHoliday(date, ["A"]), new ApplicableHoliday(date, ["B"]),
             new ApplicableHoliday(date.AddDays(-1), ["Outside"])], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Empty(result.LeaveDates);
    }

    [Fact]
    public void FindBest_UsesInclusiveLeapYearRangeOf366Days()
    {
        var result = _optimizer.FindBest(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31),
            int.MaxValue, 366, [], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Equal(366, result.Candidate.TotalDays);
        Assert.Equal(new DateOnly(2024, 2, 29), result.LeaveDates.Single(date => date.Month == 2 && date.Day == 29));
        Assert.Equal(result.Candidate.LeaveDaysRequired, result.LeaveDates.Count);
        Assert.Equal(366, result.Candidate.LeaveDaysRequired + (result.Candidate.TotalDays - result.Candidate.LeaveDaysRequired));
    }

    [Fact]
    public void FindBest_HandlesDateOnlyMinimumAndOneDayRange()
    {
        var result = _optimizer.FindBest(DateOnly.MinValue, DateOnly.MinValue, 1, 1,
            [], new LongestBreakStrategy());

        Assert.NotNull(result);
        Assert.Equal(DateOnly.MinValue, result.Candidate.StartDate);
        Assert.Equal([DateOnly.MinValue], result.LeaveDates);
    }

    [Fact]
    public void FindBest_IsIndependentOfHolidayRecordOrderAndDuplicates()
    {
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 11);
        ApplicableHoliday first = new(new DateOnly(2026, 1, 6), ["Synthetic Tuesday"]);
        ApplicableHoliday second = new(new DateOnly(2026, 1, 8), ["Synthetic Thursday"]);

        var original = _optimizer.FindBest(from, to, 2, 1, [first, second], new LongestBreakStrategy());
        var reordered = _optimizer.FindBest(from, to, 2, 1, [second, first, first], new LongestBreakStrategy());

        Assert.Equal(original?.Candidate, reordered?.Candidate);
        Assert.Equal(original?.LeaveDates, reordered?.LeaveDates);
    }

    [Fact]
    public void FindBest_BudgetIncreaseNeverShortensLongestBreak()
    {
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 11);
        var smaller = _optimizer.FindBest(from, to, 2, 1, [], new LongestBreakStrategy());
        var larger = _optimizer.FindBest(from, to, 3, 1, [], new LongestBreakStrategy());

        Assert.NotNull(smaller);
        Assert.NotNull(larger);
        Assert.True(larger.Candidate.TotalDays >= smaller.Candidate.TotalDays);
    }

    [Fact]
    public void FindBest_RejectsInvalidDomainArguments()
    {
        var date = new DateOnly(2026, 1, 5);

        Assert.Throws<ArgumentException>(() => _optimizer.FindBest(date.AddDays(1), date, 1, 1, [], new LongestBreakStrategy()));
        Assert.Throws<ArgumentOutOfRangeException>(() => _optimizer.FindBest(date, date, -1, 1, [], new LongestBreakStrategy()));
        Assert.Throws<ArgumentOutOfRangeException>(() => _optimizer.FindBest(date, date, 1, 0, [], new LongestBreakStrategy()));
        Assert.Throws<ArgumentNullException>(() => _optimizer.FindBest(date, date, 1, 1, [], null!));
    }
}
