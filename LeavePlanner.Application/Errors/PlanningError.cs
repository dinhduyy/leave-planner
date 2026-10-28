using LeavePlanner.Application.Common;

namespace LeavePlanner.Application.Errors;

public sealed record PlanningError(string Code, string Field, string Description) : Error(Code, Description);
