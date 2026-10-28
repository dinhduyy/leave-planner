using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Planning;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Domain.Tests;

public sealed class RecommendationStrategyTests
{
    private static readonly LeaveCandidate A = new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10), 10, 5);
    private static readonly LeaveCandidate B = new(new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 10), 8, 1);
    private static readonly LeaveCandidate C = new(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10), 6, 0);
    private static readonly LeaveCandidate D = new(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 7), 6, 2);

    [Fact]
    public void Strategies_ExposePrioritiesAndMinimumLengthRequirements()
    {
        IRecommendationStrategy longest = new LongestBreakStrategy();
        IRecommendationStrategy minimum = new MinimumLeaveStrategy();
        IRecommendationStrategy efficiency = new BestEfficiencyStrategy();

        Assert.Equal(PlanningPriority.LongestBreak, longest.Priority);
        Assert.Equal(PlanningPriority.MinimumLeave, minimum.Priority);
        Assert.Equal(PlanningPriority.BestEfficiency, efficiency.Priority);
        Assert.False(longest.RequiresMinTotalDays);
        Assert.True(minimum.RequiresMinTotalDays);
        Assert.True(efficiency.RequiresMinTotalDays);
    }

    [Fact]
    public void Strategies_ExplainTheRankingTheyApply()
    {
        Assert.Equal("It is the longest eligible continuous break; ties use fewer leave days, then an earlier start.",
            new LongestBreakStrategy().DescribeSelection(A, 1));
        Assert.Equal("It uses the fewest leave days among breaks of at least 7 days; ties use a longer break, then an earlier start.",
            new MinimumLeaveStrategy().DescribeSelection(B, 7));
        Assert.Equal("It is the longest break of at least 3 days that uses no leave.",
            new BestEfficiencyStrategy().DescribeSelection(C, 3));
        Assert.Equal("It has the highest total-days-per-leave-day ratio among eligible breaks of at least 3 days; ties use a longer break, fewer leave days, then an earlier start.",
            new BestEfficiencyStrategy().DescribeSelection(B, 3));
    }

    [Fact]
    public void LongestBreak_RanksDurationThenLeaveThenEarlierStart()
    {
        var strategy = new LongestBreakStrategy();

        Assert.True(strategy.IsBetter(A, B));
        Assert.True(strategy.IsBetter(new LeaveCandidate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), 10, 4), A));
        Assert.True(strategy.IsBetter(A, new LeaveCandidate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), 10, 5)));
        Assert.False(strategy.IsBetter(A, A));
    }

    [Fact]
    public void MinimumLeave_RanksLeaveThenDurationThenEarlierStart()
    {
        var strategy = new MinimumLeaveStrategy();

        Assert.True(strategy.IsBetter(C, B));
        Assert.True(strategy.IsBetter(B, A));
        Assert.True(strategy.IsBetter(B, new LeaveCandidate(new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 10), 7, 1)));
        Assert.True(strategy.IsBetter(D, new LeaveCandidate(new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 8), 6, 2)));
        Assert.False(strategy.IsBetter(B, B));
    }

    [Fact]
    public void BestEfficiency_RanksZeroLeaveAbovePositiveThenExactRatioAndTies()
    {
        var strategy = new BestEfficiencyStrategy();

        Assert.True(strategy.IsBetter(C, B));
        Assert.True(strategy.IsBetter(B, A)); // 8/1 > 10/5
        Assert.True(strategy.IsBetter(new LeaveCandidate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 9), 8, 2), D)); // 8/2 > 6/2
        Assert.True(strategy.IsBetter(new LeaveCandidate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), 8, 2), new LeaveCandidate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 5), 4, 1))); // equal ratio, longer
        Assert.True(strategy.IsBetter(new LeaveCandidate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), 6, 0), C)); // zero leave, earlier start
        Assert.False(strategy.IsBetter(B, B));
    }

    [Fact]
    public void BestEfficiency_UsesWideCrossProductForLargeCandidateCounts()
    {
        var strategy = new BestEfficiencyStrategy();
        var greaterRatio = new LeaveCandidate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 100_000, 70_000);
        var lowerRatio = new LeaveCandidate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2), 90_000, 70_000);

        Assert.True(strategy.IsBetter(greaterRatio, lowerRatio));
    }
}
