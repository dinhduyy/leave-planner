using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Contracts;

public sealed record CalendarPolicyResponse(string WorkingDays, string WeekendDays, string HolidayType, string Scope);

public sealed record PublicHolidayResponse(DateOnly Date, IReadOnlyList<string> Names);

public sealed record LeaveRecommendationResponse(
    DateOnly StartDate, DateOnly EndDate, int TotalDays, int LeaveDaysRequired,
    int NonLeaveDays, IReadOnlyList<DateOnly> LeaveDates, IReadOnlyList<PublicHolidayResponse> PublicHolidays);

public sealed record RecommendationResponse(
    string CountryCode, string? SubdivisionCode, DateOnly FromDate, DateOnly ToDate,
    int LeaveBudget, PlanningPriority Priority, int MinTotalDays, CalendarPolicyResponse Calendar,
    LeaveRecommendationResponse? Recommendation, string Explanation,
    IReadOnlyList<string> Assumptions, string HolidaySource);
