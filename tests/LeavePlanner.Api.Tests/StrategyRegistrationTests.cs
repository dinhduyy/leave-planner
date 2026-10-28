using LeavePlanner.Configuration;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeavePlanner.Api.Tests;

public sealed class StrategyRegistrationTests
{
    [Fact]
    public void ResolvesRegisteredPriorities()
    {
        using var provider = new ServiceCollection().AddLeavePlanning().BuildServiceProvider();

        var strategies = provider.GetRequiredService<IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy>>();

        Assert.IsType<LongestBreakStrategy>(strategies[PlanningPriority.LongestBreak]);
        Assert.IsType<MinimumLeaveStrategy>(strategies[PlanningPriority.MinimumLeave]);
        Assert.IsType<BestEfficiencyStrategy>(strategies[PlanningPriority.BestEfficiency]);
        Assert.Null(strategies.GetValueOrDefault((PlanningPriority)999));
    }

    [Fact]
    public void DuplicateStrategyRegistrationFails()
    {
        var services = new ServiceCollection().AddLeavePlanning();
        services.AddSingleton<IRecommendationStrategy, LongestBreakStrategy>();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy>>());
    }

    [Fact]
    public void EmptyStrategyRegistrationFails()
    {
        var services = new ServiceCollection().AddLeavePlanning();
        services.RemoveAll<IRecommendationStrategy>();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy>>());
    }

    [Fact]
    public void MissingDefaultStrategyFails()
    {
        var services = new ServiceCollection().AddLeavePlanning();
        services.RemoveAll<IRecommendationStrategy>();
        services.AddSingleton<IRecommendationStrategy, BestEfficiencyStrategy>();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy>>());
    }
}
