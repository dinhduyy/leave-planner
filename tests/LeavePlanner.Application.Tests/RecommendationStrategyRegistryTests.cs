using LeavePlanner.Domain.Enums;
using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Application.Tests;

public sealed class RecommendationStrategyRegistryTests
{
    [Fact]
    public void ResolvesDefaultAndEnumPriorities()
    {
        var registry = CreateRegistry();

        Assert.IsType<LongestBreakStrategy>(registry.Find(null));
        Assert.IsType<MinimumLeaveStrategy>(registry.Find(PlanningPriority.MinimumLeave));
        Assert.IsType<BestEfficiencyStrategy>(registry.Find(PlanningPriority.BestEfficiency));
        Assert.Null(registry.Find((PlanningPriority)999));
    }

    [Fact]
    public void RejectsUnregisteredPriority()
    {
        var registry = CreateRegistry();

        Assert.IsType<BestEfficiencyStrategy>(registry.Get(PlanningPriority.BestEfficiency));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Get((PlanningPriority)999));
    }

    [Fact]
    public void DuplicateStrategyRegistrationFailsAtStartup()
    {
        Assert.Throws<ArgumentException>(() => new RecommendationStrategyRegistry([
            new LongestBreakStrategy(), new LongestBreakStrategy()
        ]));
    }

    [Fact]
    public void EmptyStrategyRegistrationFailsAtStartup()
    {
        Assert.Throws<ArgumentException>(() => new RecommendationStrategyRegistry([]));
    }

    [Fact]
    public void MissingDefaultStrategyFailsAtStartup()
    {
        Assert.Throws<ArgumentException>(() => new RecommendationStrategyRegistry([new BestEfficiencyStrategy()]));
    }

    private static RecommendationStrategyRegistry CreateRegistry() => new([
        new LongestBreakStrategy(), new MinimumLeaveStrategy(), new BestEfficiencyStrategy()
    ]);
}
