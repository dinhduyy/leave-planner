using LeavePlanner.Infrastructure.ReferenceData;

namespace LeavePlanner.Infrastructure.Tests;

public sealed class IsoSubdivisionCatalogTests
{
    private readonly IsoSubdivisionCatalog _catalog = new();

    [Theory]
    [InlineData("AT", "AT-9")]
    [InlineData("US", "US-CA")]
    [InlineData("AF", "AF-BDS")]
    public void Contains_AcceptsTopLevelSubdivisionCodes(string country, string subdivision)
    {
        Assert.True(_catalog.Contains(country, subdivision));
    }

    [Fact]
    public void Contains_RejectsCodeBelongingToAnotherCountry()
    {
        Assert.False(_catalog.Contains("AT", "US-CA"));
    }

    [Fact]
    public void Contains_RejectsChildLevelCode()
    {
        // AZ-BAB has AZ-NX as its parent in the pinned source CSV.
        Assert.False(_catalog.Contains("AZ", "AZ-BAB"));
    }

    [Theory]
    [InlineData("US", "US-XX")]
    [InlineData("US", "CA")]
    [InlineData("US", "")]
    public void Contains_RejectsUnknownOrIncompleteCode(string country, string subdivision)
    {
        Assert.False(_catalog.Contains(country, subdivision));
    }

    [Fact]
    public void Contains_NormalizesWhitespaceAndCase()
    {
        Assert.True(_catalog.Contains(" us ", " us-ca "));
    }

    [Fact]
    public void Contains_RejectsUnknownCountryEvenForKnownSubdivision()
    {
        Assert.False(_catalog.Contains("ZZ", "US-CA"));
    }
}
