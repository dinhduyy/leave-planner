using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Abstractions;
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
        services.AddSingleton<RecommendationStrategyRegistry>();
        services.AddScoped<LeavePlanningService>();
        services.AddSingleton<RecommendationResponseMapper>();
        return services;
    }
}
