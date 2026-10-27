namespace LeavePlanner.Application.Errors;

public enum HolidaySourceFailure
{
    UnsupportedYear,
    BadData,
    Unavailable,
    Timeout
}

public sealed class HolidaySourceException : Exception
{
    public HolidaySourceException(HolidaySourceFailure failure, string message, int? year = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        Year = year;
    }

    public HolidaySourceFailure Failure { get; }

    public int? Year { get; }
}
