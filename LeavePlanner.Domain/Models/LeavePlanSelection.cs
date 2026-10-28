namespace LeavePlanner.Domain.Models;

public sealed record LeavePlanSelection(LeaveCandidate Candidate, IReadOnlyList<DateOnly> LeaveDates);
