using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Planning;

namespace LeavePlanner.Domain.Strategies;

public sealed class BestEfficiencyStrategy : IRecommendationStrategy
{
    public PlanningPriority Priority => PlanningPriority.BestEfficiency;

    public bool RequiresMinTotalDays => true;

    public bool IsBetter(LeaveCandidate candidate, LeaveCandidate incumbent)
    {
        var candidateNeedsNoLeave = candidate.LeaveDaysRequired == 0;
        var incumbentNeedsNoLeave = incumbent.LeaveDaysRequired == 0;
        if (candidateNeedsNoLeave != incumbentNeedsNoLeave)
            return candidateNeedsNoLeave;

        if (candidateNeedsNoLeave)
        {
            if (candidate.TotalDays != incumbent.TotalDays)
                return candidate.TotalDays > incumbent.TotalDays;

            return candidate.StartDate < incumbent.StartDate;
        }

        // Cross multiplication compares ratios exactly and avoids division by zero.
        var candidateProduct = (long)candidate.TotalDays * incumbent.LeaveDaysRequired;
        var incumbentProduct = (long)incumbent.TotalDays * candidate.LeaveDaysRequired;
        if (candidateProduct != incumbentProduct)
            return candidateProduct > incumbentProduct;

        if (candidate.TotalDays != incumbent.TotalDays)
            return candidate.TotalDays > incumbent.TotalDays;

        if (candidate.LeaveDaysRequired != incumbent.LeaveDaysRequired)
            return candidate.LeaveDaysRequired < incumbent.LeaveDaysRequired;

        return candidate.StartDate < incumbent.StartDate;
    }

    public string DescribeSelection(LeaveCandidate candidate, int minTotalDays) =>
        candidate.LeaveDaysRequired == 0
            ? $"It is the longest break of at least {minTotalDays} days that uses no leave."
            : $"It has the highest total-days-per-leave-day ratio among eligible breaks of at least {minTotalDays} days; ties use a longer break, fewer leave days, then an earlier start.";
}
