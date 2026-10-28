using System.Collections.Frozen;
using LeavePlanner.Application.Abstractions;
using LeavePlanner.Infrastructure.Constants;

namespace LeavePlanner.Infrastructure.ReferenceData;

public sealed class IsoSubdivisionCatalog : ISubdivisionCatalog
{
    private static readonly FrozenSet<string> TopLevelCodes = IsoSubdivisionData.Codes
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public bool Contains(string countryCode, string subdivisionCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(subdivisionCode))
        {
            return false;
        }

        var country = countryCode.Trim();
        var subdivision = subdivisionCode.Trim();

        return subdivision.StartsWith(country + "-", StringComparison.OrdinalIgnoreCase)
            && TopLevelCodes.Contains(subdivision);
    }
}
