using LeavePlanner.Application.Enums;
using System.Net;
using LeavePlanner.Application.Errors;

namespace LeavePlanner.Infrastructure.Nager;

internal sealed class NagerHttpTransport(HttpClient httpClient)
{
    public async Task<string> GetPayloadAsync(string path, int? year, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // ResponseContentRead buffers the body under HttpClient.Timeout, including a stalled response body.
            using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (year.HasValue && response.StatusCode == HttpStatusCode.BadRequest &&
                    NagerResponseParser.HasExplicitYearError(
                        await response.Content.ReadAsStringAsync(cancellationToken)))
                {
                    throw new HolidaySourceException(
                        HolidaySourceFailure.UnsupportedYear,
                        $"The holiday provider does not support year {year.Value}.",
                        year);
                }

                throw new HolidaySourceException(
                    HolidaySourceFailure.Unavailable,
                    "The holiday provider could not complete the request.",
                    year);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new HolidaySourceException(
                HolidaySourceFailure.Timeout,
                "The holiday provider timed out.",
                year,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new HolidaySourceException(
                HolidaySourceFailure.Unavailable,
                "The holiday provider could not be reached.",
                year,
                exception);
        }
    }
}
