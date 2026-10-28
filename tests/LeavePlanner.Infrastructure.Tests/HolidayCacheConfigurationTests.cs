using LeavePlanner.Infrastructure;
using LeavePlanner.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeavePlanner.Infrastructure.Tests;

public sealed class HolidayCacheConfigurationTests
{
    [Theory]
    [InlineData("0", "512")]
    [InlineData("-1", "512")]
    [InlineData("24", "0")]
    [InlineData("24", "-1")]
    public void RejectsNonPositiveCacheSettings(string hours, string entries)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHolidayInfrastructure(Configuration(hours, entries)));

        Assert.Contains("HolidayCache", exception.Message);
    }

    [Theory]
    [InlineData("8761")]
    [InlineData("2147483647")]
    public void RejectsExcessiveCacheLifetimeDuringRegistration(string hours)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHolidayInfrastructure(Configuration(hours, "512")));

        Assert.Contains("HolidayCache", exception.Message);
    }

    [Fact]
    public void RegistersCachedHolidaySourceForValidSettings()
    {
        var services = new ServiceCollection();
        services.AddHolidayInfrastructure(Configuration("24", "512"));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<CachedHolidaySource>(
            provider.GetRequiredService<LeavePlanner.Application.Abstractions.IHolidaySource>());
    }

    private static IConfiguration Configuration(string hours, string entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HolidayProvider:BaseUrl"] = "https://nagerholidays.com/",
            ["HolidayProvider:TimeoutSeconds"] = "10",
            ["HolidayCache:DurationHours"] = hours,
            ["HolidayCache:MaxEntries"] = entries
        }).Build();
}
