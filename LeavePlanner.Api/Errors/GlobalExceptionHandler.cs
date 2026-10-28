using LeavePlanner.Application.Enums;
using System.Text.Json;
using LeavePlanner.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LeavePlanner.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        ProblemDetails problem;
        if (exception is HolidaySourceException source)
        {
            logger.LogWarning(exception, "Holiday source failure: {Failure}, year: {Year}", source.Failure, source.Year);
            problem = source.Failure switch
            {
                HolidaySourceFailure.UnsupportedYear => Validation("fromDate",
                    $"Year {source.Year} is not supported by the holiday provider. Choose dates within its supported years."),
                HolidaySourceFailure.BadData => Create(StatusCodes.Status502BadGateway,
                    "Invalid holiday data", "The holiday provider returned data that cannot be used safely for a recommendation."),
                HolidaySourceFailure.Timeout => Create(StatusCodes.Status504GatewayTimeout,
                    "Holiday provider timeout", "The holiday provider did not respond in time. Try again later."),
                _ => Create(StatusCodes.Status503ServiceUnavailable,
                    "Holiday provider unavailable", "Holiday data is temporarily unavailable. Try again later.")
            };
        }
        else
        {
            logger.LogError(exception, "Unhandled request failure");
            problem = Create(StatusCodes.Status500InternalServerError,
                "Internal server error", "An unexpected error occurred.");
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem
            }))
        {
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, problem.GetType(),
                JsonOptions, cancellationToken);
        }

        return true;
    }

    private static ValidationProblemDetails Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };

    private static ProblemDetails Create(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
