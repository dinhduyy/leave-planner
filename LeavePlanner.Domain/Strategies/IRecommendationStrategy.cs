using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Planning;

namespace LeavePlanner.Domain.Strategies;

public interface IRecommendationStrategy
{
    PlanningPriority Priority { get; }

    bool RequiresMinTotalDays { get; }

    bool IsBetter(LeaveCandidate candidate, LeaveCandidate incumbent);

    string DescribeSelection(LeaveCandidate candidate, int minTotalDays);
}
