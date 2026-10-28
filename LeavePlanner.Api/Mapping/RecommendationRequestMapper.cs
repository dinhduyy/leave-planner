using LeavePlanner.Application.Models;
using LeavePlanner.Application.Services;
using LeavePlanner.Contracts;

namespace LeavePlanner.Mapping;

public static class RecommendationRequestMapper
{
    public static PlanningQuery Map(RecommendationRequest request, RecommendationStrategyRegistry strategies)
    {
        if (request.CountryCode is not { } country || request.FromDate is not { } fromDate ||
            request.ToDate is not { } toDate || request.LeaveBudget is not { } leaveBudget ||
            strategies.Find(request.Priority) is not { } strategy)
        {
            throw new InvalidOperationException("Only validated recommendation requests can be mapped.");
        }

        var subdivision = request.SubdivisionCode?.Trim().ToUpperInvariant();
        return new PlanningQuery(
            country.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(subdivision) ? null : subdivision,
            fromDate,
            toDate,
            leaveBudget,
            strategy.Priority,
            request.MinTotalDays ?? 1);
    }
}
