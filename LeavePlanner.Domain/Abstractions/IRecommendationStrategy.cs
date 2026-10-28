using LeavePlanner.Domain.Enums;
using LeavePlanner.Domain.Models;

namespace LeavePlanner.Domain.Abstractions;

public interface IRecommendationStrategy
{
    PlanningPriority Priority { get; }

    bool RequiresMinTotalDays { get; }

    bool IsBetter(LeaveCandidate candidate, LeaveCandidate incumbent);

    string DescribeSelection(LeaveCandidate candidate, int minTotalDays);
}
