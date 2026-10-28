using LeavePlanner.Application.Models;
using LeavePlanner.Domain.Models;

namespace LeavePlanner.Application.Helpers;

public static class HolidayCalendarNormalizer
{
    public static IReadOnlyList<ApplicableHoliday> Normalize(
        IEnumerable<HolidayRecord> records,
        string countryCode,
        string? subdivisionCode,
        DateOnly fromDate,
        DateOnly toDate)
    {
        return records
            .Where(record => record.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase))
            .Where(record => record.Date >= fromDate && record.Date <= toDate)
            .Where(record => record.HolidayTypes.Contains("Public", StringComparer.OrdinalIgnoreCase))
            .Where(record => record.NationalHoliday ||
                (subdivisionCode is not null && record.SubdivisionCodes?.Contains(subdivisionCode, StringComparer.OrdinalIgnoreCase) == true))
            .GroupBy(record => record.Date)
            .OrderBy(group => group.Key)
            .Select(group => new ApplicableHoliday(
                group.Key,
                group.Select(record => record.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }
}
