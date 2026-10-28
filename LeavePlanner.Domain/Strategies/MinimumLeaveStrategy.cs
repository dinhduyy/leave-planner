using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Planning;

namespace LeavePlanner.Domain.Strategies;

public sealed class MinimumLeaveStrategy : IRecommendationStrategy
{
    public PlanningPriority Priority => PlanningPriority.MinimumLeave;

    public bool RequiresMinTotalDays => true;

    public bool IsBetter(LeaveCandidate candidate, LeaveCandidate incumbent)
    {
        if (candidate.LeaveDaysRequired != incumbent.LeaveDaysRequired)
            return candidate.LeaveDaysRequired < incumbent.LeaveDaysRequired;

        if (candidate.TotalDays != incumbent.TotalDays)
            return candidate.TotalDays > incumbent.TotalDays;

        return candidate.StartDate < incumbent.StartDate;
    }

    public string DescribeSelection(LeaveCandidate candidate, int minTotalDays) =>
        $"It uses the fewest leave days among breaks of at least {minTotalDays} days; ties use a longer break, then an earlier start.";
}
