using LeavePlanner.Application.Enums;

namespace LeavePlanner.Application.Errors;

public sealed class HolidaySourceException(
    HolidaySourceFailure failure,
    string message,
    int? year = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public HolidaySourceFailure Failure { get; } = failure;

    public int? Year { get; } = year;
}
