namespace LeavePlanner.Application.Common;

public abstract record Result<TValue, TError>
{
    private Result() { }

    public sealed record Success(TValue Value) : Result<TValue, TError>;

    public sealed record Failure(TError Error) : Result<TValue, TError>;

    public TOutput Match<TOutput>(Func<TValue, TOutput> onSuccess, Func<TError, TOutput> onFailure) =>
        this switch
        {
            Success success => onSuccess(success.Value),
            Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Unknown result state.")
        };
}
