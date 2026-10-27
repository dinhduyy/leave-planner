using System.Globalization;
using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Models;

namespace LeavePlanner.Infrastructure.Nager;

public sealed class NagerHolidayClient(HttpClient httpClient) : IHolidaySource
{
    private readonly NagerHttpTransport _transport = new(httpClient);

    public async Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(
        string countryCode, int year, CancellationToken cancellationToken)
    {
        var path = $"api/v4/Holidays/{Uri.EscapeDataString(countryCode)}/{year.ToString(CultureInfo.InvariantCulture)}";
        var payload = await _transport.GetPayloadAsync(path, year, cancellationToken);
        return NagerResponseParser.ParseHolidays(payload, countryCode, year);
    }

    public async Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken)
    {
        var payload = await _transport.GetPayloadAsync("api/v4/Countries/Available", null, cancellationToken);
        return NagerResponseParser.ParseCountryCodes(payload);
    }
}
