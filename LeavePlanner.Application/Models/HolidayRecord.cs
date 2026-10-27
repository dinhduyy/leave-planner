namespace LeavePlanner.Application.Models;

public sealed record HolidayRecord(
    DateOnly Date,
    string Name,
    string CountryCode,
    bool NationalHoliday,
    IReadOnlyList<string>? SubdivisionCodes,
    IReadOnlyList<string> HolidayTypes);
