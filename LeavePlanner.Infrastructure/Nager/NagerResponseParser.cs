using LeavePlanner.Application.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Models;

namespace LeavePlanner.Infrastructure.Nager;

internal static class NagerResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public static IReadOnlyList<HolidayRecord> ParseHolidays(string payload, string countryCode, int year)
    {
        NagerHolidayDto?[]? holidays;
        try
        {
            holidays = JsonSerializer.Deserialize<NagerHolidayDto?[]>(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw BadData("The holiday provider returned malformed holiday data.", year, exception);
        }

        if (holidays is null)
        {
            throw BadData("The holiday provider returned no holiday list.", year);
        }

        var records = new List<HolidayRecord>(holidays.Length);
        foreach (var holiday in holidays)
        {
            records.Add(ToRecord(holiday, countryCode, year));
        }

        return records;
    }

    public static IReadOnlySet<string> ParseCountryCodes(string payload)
    {
        NagerCountryDto?[]? countries;
        try
        {
            countries = JsonSerializer.Deserialize<NagerCountryDto?[]>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            throw BadData("The holiday provider returned malformed country data.");
        }

        if (countries is null || countries.Length == 0)
        {
            throw BadData("The holiday provider returned an empty or missing country catalog.");
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var country in countries)
        {
            if (country is null || !IsCountryCode(country.CountryCode))
            {
                throw BadData("The holiday provider returned an invalid country entry.");
            }

            codes.Add(country.CountryCode);
        }

        return codes;
    }

    public static bool HasExplicitYearError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("errors", out var errors) ||
                errors.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return errors.EnumerateObject().Any(property =>
                property.Name.Equals("year", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Array &&
                property.Value.GetArrayLength() > 0);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static HolidayRecord ToRecord(NagerHolidayDto? holiday, string requestedCountry, int requestedYear)
    {
        if (holiday is null ||
            !DateOnly.TryParseExact(holiday.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            date.Year != requestedYear ||
            string.IsNullOrWhiteSpace(holiday.Name) ||
            !IsCountryCode(holiday.CountryCode) ||
            !holiday.CountryCode.Equals(requestedCountry, StringComparison.OrdinalIgnoreCase) ||
            holiday.NationalHoliday is null ||
            holiday.HolidayTypes is null ||
            holiday.HolidayTypes.Any(string.IsNullOrWhiteSpace) ||
            holiday.SubdivisionCodes?.Any(code => !IsSubdivisionOfCountry(code, requestedCountry)) == true)
        {
            throw BadData("The holiday provider returned an incomplete or inconsistent holiday entry.", requestedYear);
        }

        var isPublic = holiday.HolidayTypes.Contains("Public", StringComparer.OrdinalIgnoreCase);
        if (holiday.NationalHoliday.Value && holiday.SubdivisionCodes is { Length: > 0 } ||
            isPublic && !holiday.NationalHoliday.Value && holiday.SubdivisionCodes is not { Length: > 0 })
        {
            throw BadData("The holiday provider returned contradictory holiday scope metadata.", requestedYear);
        }

        return new HolidayRecord(
            date,
            holiday.Name,
            holiday.CountryCode,
            holiday.NationalHoliday.Value,
            holiday.SubdivisionCodes,
            holiday.HolidayTypes);
    }

    private static bool IsCountryCode([NotNullWhen(true)] string? code) =>
        code is { Length: 2 } && code.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool IsSubdivisionOfCountry(string? code, string countryCode) =>
        code is { Length: > 3 } &&
        code.StartsWith(countryCode + "-", StringComparison.OrdinalIgnoreCase) &&
        code[3..].All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9');

    private static HolidaySourceException BadData(string message, int? year = null, Exception? exception = null) =>
        new(HolidaySourceFailure.BadData, message, year, exception);

    private sealed class NagerHolidayDto
    {
        [JsonPropertyName("date")]
        public required string? Date { get; set; }

        [JsonPropertyName("name")]
        public required string? Name { get; set; }

        [JsonPropertyName("countryCode")]
        public required string? CountryCode { get; set; }

        [JsonPropertyName("nationalHoliday")]
        public required bool? NationalHoliday { get; set; }

        [JsonPropertyName("subdivisionCodes")]
        public required string[]? SubdivisionCodes { get; set; }

        [JsonPropertyName("holidayTypes")]
        public required string[]? HolidayTypes { get; set; }
    }

    private sealed class NagerCountryDto
    {
        [JsonPropertyName("countryCode")]
        public required string? CountryCode { get; set; }
    }
}
