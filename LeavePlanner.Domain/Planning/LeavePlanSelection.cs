namespace LeavePlanner.Domain.Planning;

public sealed record LeavePlanSelection(LeaveCandidate Candidate, IReadOnlyList<DateOnly> LeaveDates);
