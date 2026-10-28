using LeavePlanner.Application.Enums;
using System.Net;
using LeavePlanner.Application.Errors;
using LeavePlanner.Infrastructure.Nager;

namespace LeavePlanner.Infrastructure.Tests;

public sealed class NagerHolidayClientTests
{
    private const string ValidHoliday = """
        [{"date":"2026-01-01","name":"New Year's Day","countryCode":"AT","nationalHoliday":true,"subdivisionCodes":null,"holidayTypes":["Public"],"futureField":"ignored"}]
        """;

    [Fact]
    public async Task ParsesV4HolidayAndExpectedRequestPath()
    {
        var client = CreateClient(request =>
        {
            Assert.Equal("https://nagerholidays.com/api/v4/Holidays/AT/2026", request.RequestUri?.ToString());
            return Json(ValidHoliday);
        });

        var holiday = Assert.Single(await client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(new DateOnly(2026, 1, 1), holiday.Date);
        Assert.Equal("New Year's Day", holiday.Name);
        Assert.True(holiday.NationalHoliday);
        Assert.Null(holiday.SubdivisionCodes);
        Assert.Equal("Public", Assert.Single(holiday.HolidayTypes));
    }

    [Fact]
    public async Task EmptyHolidayArrayIsValid()
    {
        var client = CreateClient(_ => Json("[]"));
        Assert.Empty(await client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
    }

    [Fact]
    public async Task ParsesAvailableCountryCodes()
    {
        var client = CreateClient(request =>
        {
            Assert.Equal("https://nagerholidays.com/api/v4/Countries/Available", request.RequestUri?.ToString());
            return Json("""[{"countryCode":"AT","name":"Austria"},{"countryCode":"DE","name":"Germany"}]""");
        });

        var codes = await client.GetAvailableCountryCodesAsync(CancellationToken.None);
        Assert.True(codes.SetEquals(["AT", "DE"]));
    }

    [Fact]
    public async Task EmptyCountryCatalogIsBadDataRatherThanEveryCountryUnsupported()
    {
        var client = CreateClient(_ => Json("[]"));

        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() =>
            client.GetAvailableCountryCodesAsync(CancellationToken.None));

        Assert.Equal(HolidaySourceFailure.BadData, exception.Failure);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"countryCode\":\"A\"}]")]
    public async Task MalformedCountryCatalogIsBadData(string payload)
    {
        var client = CreateClient(_ => Json(payload));

        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() =>
            client.GetAvailableCountryCodesAsync(CancellationToken.None));

        Assert.Equal(HolidaySourceFailure.BadData, exception.Failure);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"New Year's Day\",\"countryCode\":\"AT\",\"subdivisionCodes\":null,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":true,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":true,\"subdivisionCodes\":null}]")]
    [InlineData("[{\"date\":\"2026-02-30\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":true,\"subdivisionCodes\":null,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2025-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":true,\"subdivisionCodes\":null,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"DE\",\"nationalHoliday\":true,\"subdivisionCodes\":null,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":false,\"subdivisionCodes\":null,\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":true,\"subdivisionCodes\":[\"AT-9\"],\"holidayTypes\":[\"Public\"]]")]
    [InlineData("[{\"date\":\"2026-01-01\",\"name\":\"Bad\",\"countryCode\":\"AT\",\"nationalHoliday\":false,\"subdivisionCodes\":[\"DE-BY\"],\"holidayTypes\":[\"Public\"]]")]
    public async Task RejectsMalformedOrContradictoryHolidayPayload(string payload)
    {
        var client = CreateClient(_ => Json(payload));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.BadData, exception.Failure);
        Assert.Equal(2026, exception.Year);
    }

    [Fact]
    public async Task AcceptsLocalHolidayWithScopeAndNewHolidayType()
    {
        var client = CreateClient(_ => Json("""[{"date":"2026-06-01","name":"Local","countryCode":"AT","nationalHoliday":false,"subdivisionCodes":["AT-9"],"holidayTypes":["FutureType","Public"]}]"""));
        var holiday = Assert.Single(await client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal("AT-9", Assert.Single(holiday.SubdivisionCodes!));
        Assert.Equal(2, holiday.HolidayTypes.Count);
    }

    [Fact]
    public async Task RejectsLocalHolidayWithForeignCountrySubdivision()
    {
        var client = CreateClient(_ => Json("""[{"date":"2026-01-01","name":"Bad","countryCode":"AT","nationalHoliday":false,"subdivisionCodes":["DE-BY"],"holidayTypes":["Public"]}]"""));

        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() =>
            client.GetHolidaysAsync("AT", 2026, CancellationToken.None));

        Assert.Equal(HolidaySourceFailure.BadData, exception.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NonSuccessWithoutYearEvidenceIsUnavailable(HttpStatusCode status)
    {
        var client = CreateClient(_ => new HttpResponseMessage(status));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task BadRequestWithoutYearEvidenceIsUnavailable()
    {
        var client = CreateClient(_ => Json("""{"errors":{"countryCode":["Invalid"]}}""", HttpStatusCode.BadRequest));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task EmptyYearValidationErrorIsNotEvidenceOfUnsupportedYear()
    {
        var client = CreateClient(_ => Json("""{"errors":{"year":[]}}""", HttpStatusCode.BadRequest));

        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() =>
            client.GetHolidaysAsync("AT", 2026, CancellationToken.None));

        Assert.Equal(HolidaySourceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task ExplicitYearValidationErrorIsUnsupportedYear()
    {
        var client = CreateClient(_ => Json("""{"errors":{"year":["Year is unsupported"]}}""", HttpStatusCode.BadRequest));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.UnsupportedYear, exception.Failure);
    }

    [Fact]
    public async Task TimeoutIsDistinguishedFromCallerCancellation()
    {
        var client = CreateClient(_ => throw new TaskCanceledException());
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.Timeout, exception.Failure);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetHolidaysAsync("AT", 2026, cancellation.Token));
    }

    [Fact]
    public async Task ConfiguredTimeoutIncludesStalledResponseBody()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StalledContent()
        });
        http.Timeout = TimeSpan.FromMilliseconds(150);
        var client = new NagerHolidayClient(http);
        using var callerCancellation = new CancellationTokenSource();

        var operation = client.GetHolidaysAsync("AT", 2026, callerCancellation.Token);
        var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(2)));
        if (completed != operation)
        {
            callerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        }

        Assert.Same(operation, completed);
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => operation);
        Assert.Equal(HolidaySourceFailure.Timeout, exception.Failure);
    }

    [Fact]
    public async Task CallerCancellationDuringStalledBodyPropagates()
    {
        using var http = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StalledContent()
        });
        http.Timeout = TimeSpan.FromSeconds(10);
        var client = new NagerHolidayClient(http);
        using var callerCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetHolidaysAsync("AT", 2026, callerCancellation.Token));
    }

    [Fact]
    public async Task RejectsInvalidAvailableCountryPayload()
    {
        var client = CreateClient(_ => Json("""[{"name":"Austria"}]"""));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetAvailableCountryCodesAsync(CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.BadData, exception.Failure);
    }

    [Fact]
    public async Task ConnectionFailureIsUnavailable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection failed"));
        var exception = await Assert.ThrowsAsync<HolidaySourceException>(() => client.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(HolidaySourceFailure.Unavailable, exception.Failure);
    }

    private static NagerHolidayClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        return new NagerHolidayClient(CreateHttpClient(response));
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new StubHandler(response)) { BaseAddress = new Uri("https://nagerholidays.com/") };

    private static HttpResponseMessage Json(string payload, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }

    private sealed class StalledContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.InfiniteTimeSpan);

        protected override Task SerializeToStreamAsync(
            Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
