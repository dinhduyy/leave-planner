using LeavePlanner.Domain.Models;

namespace LeavePlanner.Application.Models;

public sealed record PlanningResult(
    PlanningQuery Query,
    LeavePlanSelection? Recommendation,
    IReadOnlyList<ApplicableHoliday> HolidaysInRecommendation,
    string Explanation);
