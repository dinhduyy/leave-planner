namespace LeavePlanner.Application.Errors;

public sealed class InvalidPlanningRequestException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
