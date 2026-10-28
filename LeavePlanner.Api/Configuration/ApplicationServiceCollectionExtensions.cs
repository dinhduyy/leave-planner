using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Strategies;
using LeavePlanner.Mapping;

namespace LeavePlanner.Configuration;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddLeavePlanning(this IServiceCollection services)
    {
        services.AddSingleton<IRecommendationStrategy, LongestBreakStrategy>();
        services.AddSingleton<IRecommendationStrategy, MinimumLeaveStrategy>();
        services.AddSingleton<IRecommendationStrategy, BestEfficiencyStrategy>();
        services.AddSingleton<IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy>>(provider =>
        {
            var strategies = provider.GetServices<IRecommendationStrategy>()
                .ToDictionary(strategy => strategy.Priority);
            if (strategies.Count == 0)
            {
                throw new ArgumentException("At least one recommendation strategy must be registered.", nameof(strategies));
            }

            if (!strategies.ContainsKey(PlanningPriority.LongestBreak))
            {
                throw new ArgumentException("The default longest-break strategy must be registered.", nameof(strategies));
            }

            return strategies.AsReadOnly();
        });
        services.AddScoped<LeavePlanningService>();
        services.AddSingleton<RecommendationResponseMapper>();
        return services;
    }
}
