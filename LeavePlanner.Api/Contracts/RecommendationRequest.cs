using LeavePlanner.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using LeavePlanner.Application.Services;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Contracts;

public sealed record RecommendationRequest : IValidatableObject
{
    [Required(ErrorMessage = "Use a two-letter ISO country code, such as AT.")]
    [RegularExpression(@"^\s*[A-Za-z]{2}\s*$", ErrorMessage = "Use a two-letter ISO country code, such as AT.")]
    public string? CountryCode { get; init; }

    public string? SubdivisionCode { get; init; }

    [Required(ErrorMessage = "Provide fromDate in YYYY-MM-DD format.")]
    public DateOnly? FromDate { get; init; }

    [Required(ErrorMessage = "Provide toDate in YYYY-MM-DD format.")]
    public DateOnly? ToDate { get; init; }

    [Required(ErrorMessage = "Provide a non-negative integer leaveBudget; zero is allowed.")]
    [Range(0, int.MaxValue, ErrorMessage = "leaveBudget must be zero or greater.")]
    public int? LeaveBudget { get; init; }

    public PlanningPriority? Priority { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "minTotalDays must be at least 1.")]
    public int? MinTotalDays { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate is { } fromDate && ToDate is { } toDate)
        {
            var length = toDate.DayNumber - fromDate.DayNumber + 1;
            if (length <= 0)
            {
                yield return new ValidationResult("toDate must be on or after fromDate.", ["toDate"]);
            }
            else if (length > 366)
            {
                yield return new ValidationResult("The inclusive date range cannot exceed 366 days.", ["toDate"]);
            }

            if (MinTotalDays is { } minimum && minimum > length)
            {
                yield return new ValidationResult("minTotalDays must be between 1 and the inclusive date range length.", ["minTotalDays"]);
            }
        }

        var strategies = (RecommendationStrategyRegistry?)validationContext.GetService(typeof(RecommendationStrategyRegistry))
            ?? throw new InvalidOperationException("Recommendation strategies are not registered for request validation.");
        var strategy = strategies.Find(Priority);
        if (strategy is null)
        {
            yield return new ValidationResult("Use longestBreak, minimumLeave, or bestEfficiency.", ["priority"]);
        }
        else if (strategy.RequiresMinTotalDays && MinTotalDays is null)
        {
            yield return new ValidationResult("Provide minTotalDays for this priority.", ["minTotalDays"]);
        }
    }
}
