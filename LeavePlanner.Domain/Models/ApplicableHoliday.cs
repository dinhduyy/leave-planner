namespace LeavePlanner.Domain.Models;

public sealed record ApplicableHoliday(DateOnly Date, IReadOnlyList<string> Names);
