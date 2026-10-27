using LeavePlanner.Application.Models;
using LeavePlanner.Contracts;

namespace LeavePlanner.Mapping;

public sealed class RecommendationResponseMapper(HolidaySourceMetadata sourceMetadata)
{
    public RecommendationResponse Map(PlanningResult result)
    {
        var query = result.Query;
        var selection = result.Recommendation;
        var scope = query.SubdivisionCode is null ? "national-only" : "national-and-subdivision";
        var assumptions = new[]
        {
            "Monday-Friday work schedule; Saturday-Sunday weekends.",
            $"Only applicable Public holidays from {sourceMetadata.PublicHolidayDataName} reduce leave days.",
            query.SubdivisionCode is null
                ? "National holidays only; local holidays are excluded because no subdivision was selected."
                : $"National and provider-listed holidays matching {query.SubdivisionCode}; local coverage depends on the provider.",
            "Company-specific leave and observed-holiday policies are excluded; this recommendation is not leave approval."
        };

        LeaveRecommendationResponse? recommendation = null;
        if (selection is not null)
        {
            var candidate = selection.Candidate;
            recommendation = new LeaveRecommendationResponse(
                candidate.StartDate, candidate.EndDate, candidate.TotalDays, candidate.LeaveDaysRequired,
                candidate.TotalDays - candidate.LeaveDaysRequired, selection.LeaveDates,
                result.HolidaysInRecommendation.Select(holiday =>
                    new PublicHolidayResponse(holiday.Date, holiday.Names)).ToArray());
        }

        return new RecommendationResponse(
            query.CountryCode, query.SubdivisionCode, query.FromDate, query.ToDate, query.LeaveBudget,
            query.Priority,
            query.MinTotalDays,
            new CalendarPolicyResponse("Monday-Friday", "Saturday-Sunday", "Public", scope),
            recommendation, result.Explanation, assumptions, sourceMetadata.Name);
    }
}
