using System.Collections.Immutable;
using System.Globalization;
using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Models;

namespace LeavePlanner.Infrastructure.Caching;

internal sealed class CachedHolidaySource(IHolidaySource source, HolidaySourceCache cache) : IHolidaySource
{
    public async Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken) =>
        await cache.GetOrLoadAsync("nager:v4:countries", async token =>
        {
            var countries = await source.GetAvailableCountryCodesAsync(token);
            return countries.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        }, cancellationToken);

    public async Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(
        string countryCode, int year, CancellationToken cancellationToken)
    {
        var normalizedCountry = countryCode.ToUpperInvariant();

        var key = $"nager:v4:holidays:{normalizedCountry}:{year.ToString(CultureInfo.InvariantCulture)}";

        return await cache.GetOrLoadAsync(key, async token =>
        {
            var holidays = await source.GetHolidaysAsync(normalizedCountry, year, token);

            return holidays.Select(record => new HolidayRecord(
                record.Date,
                record.Name,
                record.CountryCode,
                record.NationalHoliday,
                record.SubdivisionCodes?.ToImmutableArray(),
                record.HolidayTypes.ToImmutableArray())).ToImmutableList();
        }, cancellationToken);
    }
}
