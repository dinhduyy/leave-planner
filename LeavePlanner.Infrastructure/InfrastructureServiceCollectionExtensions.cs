using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Models;
using LeavePlanner.Infrastructure.Caching;
using LeavePlanner.Infrastructure.Nager;
using LeavePlanner.Infrastructure.ReferenceData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeavePlanner.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const int MaximumCacheDurationHours = 24 * 365;

    public static IServiceCollection AddHolidayInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["HolidayProvider:BaseUrl"]
            ?? throw new InvalidOperationException("HolidayProvider:BaseUrl is required.");

        var timeoutSeconds = configuration.GetValue<int>("HolidayProvider:TimeoutSeconds");

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress) ||
            baseAddress.Scheme != Uri.UriSchemeHttps || timeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "HolidayProvider configuration needs an HTTPS BaseUrl and a positive TimeoutSeconds.");
        }

        var cacheDurationHours = configuration.GetValue<int>("HolidayCache:DurationHours");

        var cacheMaxEntries = configuration.GetValue<int>("HolidayCache:MaxEntries");

        if (cacheDurationHours <= 0 || cacheDurationHours > MaximumCacheDurationHours ||
            cacheMaxEntries <= 0)
        {
            throw new InvalidOperationException(
                "HolidayCache configuration needs DurationHours from 1 to 8760 and a positive MaxEntries.");
        }

        var cacheDuration = TimeSpan.FromHours(cacheDurationHours);

        services.AddHttpClient<NagerHolidayClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddSingleton(_ => new HolidaySourceCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = cacheMaxEntries }),
            cacheDuration));

        services.AddTransient<IHolidaySource>(provider => new CachedHolidaySource(
            provider.GetRequiredService<NagerHolidayClient>(),
            provider.GetRequiredService<HolidaySourceCache>()));

        services.AddSingleton(new HolidaySourceMetadata(
            "Nager.Holidays Community API v4", "Nager.Holidays v4"));

        services.AddSingleton<ISubdivisionCatalog, IsoSubdivisionCatalog>();

        return services;
    }
}
