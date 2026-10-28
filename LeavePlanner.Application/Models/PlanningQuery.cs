using LeavePlanner.Domain.Enums;

namespace LeavePlanner.Application.Models;

public sealed record PlanningQuery(
    string CountryCode,
    string? SubdivisionCode,
    DateOnly FromDate,
    DateOnly ToDate,
    int LeaveBudget,
    PlanningPriority Priority,
    int MinTotalDays);
