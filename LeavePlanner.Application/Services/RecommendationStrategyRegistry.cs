using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Enums;

namespace LeavePlanner.Application.Services;

public sealed class RecommendationStrategyRegistry
{
    private readonly IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy> _byPriority;

    public RecommendationStrategyRegistry(IEnumerable<IRecommendationStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        var registered = strategies.ToArray();
        if (registered.Length == 0)
        {
            throw new ArgumentException("At least one recommendation strategy must be registered.", nameof(strategies));
        }

        _byPriority = registered.ToDictionary(strategy => strategy.Priority);
        if (!_byPriority.ContainsKey(PlanningPriority.LongestBreak))
        {
            throw new ArgumentException("The default longest-break strategy must be registered.", nameof(strategies));
        }
    }

    public IRecommendationStrategy? Find(PlanningPriority? priority) =>
        _byPriority.TryGetValue(priority ?? PlanningPriority.LongestBreak, out var strategy) ? strategy : null;

    public IRecommendationStrategy Get(PlanningPriority priority) =>
        _byPriority.TryGetValue(priority, out var strategy)
            ? strategy
            : throw new ArgumentOutOfRangeException(nameof(priority), priority,
                "No recommendation strategy is registered for this priority.");
}
