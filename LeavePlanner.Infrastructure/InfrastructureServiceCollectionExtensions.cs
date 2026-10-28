using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Models;
using LeavePlanner.Infrastructure.Nager;
using LeavePlanner.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeavePlanner.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
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

        services.AddHttpClient<NagerHolidayClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });
        services.AddTransient<IHolidaySource>(provider => provider.GetRequiredService<NagerHolidayClient>());
        services.AddSingleton(new HolidaySourceMetadata(
            "Nager.Holidays Community API v4", "Nager.Holidays v4"));
        services.AddSingleton<ISubdivisionCatalog, IsoSubdivisionCatalog>();
        return services;
    }
}
