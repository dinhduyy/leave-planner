using LeavePlanner.Application.Enums;
using LeavePlanner.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeavePlanner.Api.Tests;

public sealed class RecommendationEndpointTests
{
    private static readonly DateOnly Friday = new(2026, 5, 1);

    [Fact]
    public async Task OpenApiDescribesPriorityAsStringEnum()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PlanningPriority");

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(["longestBreak", "minimumLeave", "bestEfficiency"],
            schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var nullableSchema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("NullableOfPlanningPriority");
        Assert.Equal("string", nullableSchema.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReturnsActionableRecommendationWithNationalOnlyScope()
    {
        using var factory = Factory((_, _) => [new HolidayRecord(Friday, "Synthetic Public Holiday", "AT", true, null, ["Public"])]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        Assert.Equal("longestBreak", root.GetProperty("priority").GetString());
        Assert.Equal("national-only", root.GetProperty("calendar").GetProperty("scope").GetString());
        Assert.Equal(1, root.GetProperty("recommendation").GetProperty("totalDays").GetInt32());
        Assert.Equal(0, root.GetProperty("recommendation").GetProperty("leaveDaysRequired").GetInt32());
        Assert.Empty(root.GetProperty("recommendation").GetProperty("leaveDates").EnumerateArray());
        Assert.Equal("Synthetic Public Holiday", root.GetProperty("recommendation").GetProperty("publicHolidays")[0].GetProperty("names")[0].GetString());
        Assert.Equal("Nager.Holidays Community API v4", root.GetProperty("holidaySource").GetString());
        Assert.Equal(
            "It is the longest eligible continuous break; ties use fewer leave days, then an earlier start. The break has 1 calendar days, requires 0 leave days, and includes 1 days covered by weekends or applicable public holidays.",
            root.GetProperty("explanation").GetString());
    }

    [Fact]
    public async Task MissingMinimumForEfficiencyReturnsFieldValidationError()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request() with { Priority = "bestEfficiency" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("minTotalDays", out _));
        Assert.Equal("One or more validation errors occurred.", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", body.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UnsupportedCountryReturnsValidationProblem()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request() with { CountryCode = "ZZ" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("countryCode", out _));
    }

    [Fact]
    public async Task UnsupportedSubdivisionReturnsValidationProblem()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request() with { SubdivisionCode = "AT-0" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("subdivisionCode", out _));
    }

    [Fact]
    public async Task UnexpectedFailureReturnsSanitizedProblem()
    {
        using var factory = Factory((_, _) => throw new InvalidOperationException("Secret synthetic details"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":500", body);
        Assert.Contains("\"title\":\"Internal server error\"", body);
        Assert.DoesNotContain("Secret synthetic details", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyCalendarWithZeroBudgetReturnsNullRecommendation()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("recommendation").ValueKind);
        Assert.Contains("budget", body.RootElement.GetProperty("explanation").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedSecondYearNeverReturnsPartialRecommendation()
    {
        using var factory = Factory((_, year) => year == 2027
            ? throw new HolidaySourceException(HolidaySourceFailure.Unavailable, "Synthetic upstream failure")
            : []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var request = Request() with
        {
            FromDate = new DateOnly(2026, 12, 31),
            ToDate = new DateOnly(2027, 1, 1),
            LeaveBudget = 2
        };

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(body.RootElement.TryGetProperty("recommendation", out _));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("null", null)]
    [InlineData("{", null)]
    [InlineData("{}", "countryCode")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\"}", "leaveBudget")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"not-a-date\",\"toDate\":\"2026-05-01\",\"leaveBudget\":0}", "fromDate")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\",\"leaveBudget\":\"zero\"}", "leaveBudget")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\",\"leaveBudget\":99999999999999999999}", "leaveBudget")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\",\"leaveBudget\":-1}", "leaveBudget")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\",\"leaveBudget\":0,\"priority\":\"unknown\"}", "priority")]
    [InlineData("{\"countryCode\":\"AT\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\",\"leaveBudget\":0,\"priority\":1}", "priority")]
    public async Task InvalidJsonOrBindingReturnsValidationProblem(string json, string? expectedField)
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsync("/api/leave-plans/recommendation",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        if (expectedField is not null)
        {
            Assert.Contains(body.RootElement.GetProperty("errors").EnumerateObject(), error =>
                error.Name.Contains(expectedField, StringComparison.OrdinalIgnoreCase) &&
                error.Value.EnumerateArray().Any(message => !string.IsNullOrWhiteSpace(message.GetString())));
        }
    }

    [Fact]
    public async Task AnnotationErrorsUseJsonFieldNames()
    {
        using var factory = Factory((_, _) => []);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsync("/api/leave-plans/recommendation",
            new StringContent("{\"countryCode\":\"A1\",\"fromDate\":\"2026-05-01\",\"toDate\":\"2026-05-01\"}",
                Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("countryCode", out _));
        Assert.True(errors.TryGetProperty("leaveBudget", out _));
        Assert.False(errors.TryGetProperty("CountryCode", out _));
        Assert.False(errors.TryGetProperty("LeaveBudget", out _));
    }

    [Fact]
    public async Task ProviderUnsupportedYearReturnsDateFieldError()
    {
        using var factory = Factory((_, year) => throw new HolidaySourceException(
            HolidaySourceFailure.UnsupportedYear, "Synthetic year error", year));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request() with
        {
            FromDate = new DateOnly(2024, 5, 1), ToDate = new DateOnly(2024, 5, 1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("fromDate", out _));
    }

    [Theory]
    [InlineData(HolidaySourceFailure.BadData, HttpStatusCode.BadGateway)]
    [InlineData(HolidaySourceFailure.Timeout, HttpStatusCode.GatewayTimeout)]
    public async Task ProviderFailureUsesSanitizedProblem(HolidaySourceFailure failure, HttpStatusCode expectedStatus)
    {
        using var factory = Factory((_, _) => throw new HolidaySourceException(failure, "Secret synthetic upstream details"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", Request());

        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secret synthetic upstream details", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentRequestsKeepNationalAndRegionalCalendarsSeparate()
    {
        using var factory = Factory((_, _) => [new HolidayRecord(Friday, "Regional", "AT", false, ["AT-9"], ["Public"])]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
            client.PostAsJsonAsync("/api/leave-plans/recommendation", Request() with
            {
                SubdivisionCode = index % 2 == 0 ? "AT-9" : null
            })));

        for (var index = 0; index < responses.Length; index++)
        {
            using var response = responses[index];
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal(index % 2 == 0 ? JsonValueKind.Object : JsonValueKind.Null,
                body.RootElement.GetProperty("recommendation").ValueKind);
        }
    }

    [Theory]
    [InlineData("longestBreak", 4, "2026-05-01", "2026-05-07", 7, 4)]
    [InlineData("minimumLeave", 4, "2026-05-01", "2026-05-04", 4, 1)]
    [InlineData("bestEfficiency", 4, "2026-05-01", "2026-05-04", 4, 1)]
    [InlineData("bestEfficiency", 3, "2026-05-01", "2026-05-03", 3, 0)]
    public async Task PriorityChangesRecommendationForSameSyntheticCalendar(
        string priority, int minimum, string expectedStart, string expectedEnd, int expectedDays, int expectedLeave)
    {
        using var factory = Factory((_, _) => [new HolidayRecord(Friday, "Synthetic Public Holiday", "AT", true, null, ["Public"])]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var request = Request() with
        {
            ToDate = new DateOnly(2026, 5, 10), LeaveBudget = 4,
            Priority = priority, MinTotalDays = minimum
        };

        var response = await client.PostAsJsonAsync("/api/leave-plans/recommendation", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        var recommendation = root.GetProperty("recommendation");
        Assert.Equal(priority, root.GetProperty("priority").GetString());
        Assert.Equal(minimum, root.GetProperty("minTotalDays").GetInt32());
        Assert.Equal(expectedStart, recommendation.GetProperty("startDate").GetString());
        Assert.Equal(expectedEnd, recommendation.GetProperty("endDate").GetString());
        Assert.Equal(expectedDays, recommendation.GetProperty("totalDays").GetInt32());
        Assert.Equal(expectedLeave, recommendation.GetProperty("leaveDaysRequired").GetInt32());
        Assert.Equal(expectedLeave, recommendation.GetProperty("leaveDates").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("explanation").GetString()));
    }

    private static TestFactory Factory(Func<string, int, IReadOnlyList<HolidayRecord>> fetch) => new(fetch);

    private static TestRequest Request() => new("AT", null, Friday, Friday, 0, null, null);

    private sealed record TestRequest(string CountryCode, string? SubdivisionCode, DateOnly FromDate,
        DateOnly ToDate, int LeaveBudget, string? Priority, int? MinTotalDays);

    private sealed class TestFactory(Func<string, int, IReadOnlyList<HolidayRecord>> fetch) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHolidaySource>();
                services.RemoveAll<ISubdivisionCatalog>();
                services.AddSingleton<IHolidaySource>(new StubHolidays(fetch));
                services.AddSingleton<ISubdivisionCatalog>(new StubSubdivisions());
            });
        }
    }

    private sealed class StubHolidays(Func<string, int, IReadOnlyList<HolidayRecord>> fetch) : IHolidaySource
    {
        public Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["AT"], StringComparer.Ordinal));

        public Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(string countryCode, int year, CancellationToken cancellationToken) =>
            Task.FromResult(fetch(countryCode, year));
    }

    private sealed class StubSubdivisions : ISubdivisionCatalog
    {
        public bool Contains(string countryCode, string subdivisionCode) => subdivisionCode == "AT-9";
    }
}
