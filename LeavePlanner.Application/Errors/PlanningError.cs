using LeavePlanner.Application.Common;

namespace LeavePlanner.Application.Errors;

public abstract record PlanningError(string Code, string Description) : Error(Code, Description);
