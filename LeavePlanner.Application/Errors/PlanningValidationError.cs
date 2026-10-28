namespace LeavePlanner.Application.Errors;

public sealed record PlanningValidationError(string Code, string Field, string Description)
    : PlanningError(Code, Description);
