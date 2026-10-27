using LeavePlanner.Application.Models;

namespace LeavePlanner.Application.Abstractions;

public interface IHolidaySource
{
    Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(
        string countryCode, int year, CancellationToken cancellationToken);
}
