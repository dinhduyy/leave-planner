using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Planning;

namespace LeavePlanner.Domain.Strategies;

public sealed class LongestBreakStrategy : IRecommendationStrategy
{
    public PlanningPriority Priority => PlanningPriority.LongestBreak;

    public bool RequiresMinTotalDays => false;

    public bool IsBetter(LeaveCandidate candidate, LeaveCandidate incumbent)
    {
        if (candidate.TotalDays != incumbent.TotalDays)
            return candidate.TotalDays > incumbent.TotalDays;

        if (candidate.LeaveDaysRequired != incumbent.LeaveDaysRequired)
            return candidate.LeaveDaysRequired < incumbent.LeaveDaysRequired;

        return candidate.StartDate < incumbent.StartDate;
    }

    public string DescribeSelection(LeaveCandidate candidate, int minTotalDays) =>
        "It is the longest eligible continuous break; ties use fewer leave days, then an earlier start.";
}
