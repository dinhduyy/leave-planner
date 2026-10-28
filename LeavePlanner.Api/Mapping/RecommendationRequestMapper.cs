using LeavePlanner.Application.Models;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Enums;
using LeavePlanner.Dtos;

namespace LeavePlanner.Mapping;

public static class RecommendationRequestMapper
{
    public static PlanningQuery Map(RecommendationRequest request,
        IReadOnlyDictionary<PlanningPriority, IRecommendationStrategy> strategies)
    {
        if (request.CountryCode is not { } country || request.FromDate is not { } fromDate ||
            request.ToDate is not { } toDate || request.LeaveBudget is not { } leaveBudget ||
            !strategies.TryGetValue(request.Priority ?? PlanningPriority.LongestBreak, out var strategy))
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
