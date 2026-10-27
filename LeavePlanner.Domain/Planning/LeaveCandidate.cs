namespace LeavePlanner.Domain.Planning;

public sealed record LeaveCandidate(DateOnly StartDate, DateOnly EndDate, int TotalDays, int LeaveDaysRequired);
