namespace LeavePlanner.Domain.Calendar;

public sealed record ApplicableHoliday(DateOnly Date, IReadOnlyList<string> Names);
