namespace LeavePlanner.Domain.Models;

public sealed record LeaveCandidate(DateOnly StartDate, DateOnly EndDate, int TotalDays, int LeaveDaysRequired);
