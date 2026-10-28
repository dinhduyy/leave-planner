using LeavePlanner.Application.Models;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Models;

namespace LeavePlanner.Application.Services;

public static class RecommendationExplanationBuilder
{
    public static string Build(PlanningQuery query, LeavePlanSelection selection, IRecommendationStrategy strategy)
    {
        var candidate = selection.Candidate;
        var nonLeaveDays = candidate.TotalDays - candidate.LeaveDaysRequired;
        var reason = strategy.DescribeSelection(candidate, query.MinTotalDays);

        return $"{reason} The break has {candidate.TotalDays} calendar days, requires {candidate.LeaveDaysRequired} leave days, and includes {nonLeaveDays} days covered by weekends or applicable public holidays.";
    }
}
