using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Models;
using LeavePlanner.Domain.Planning;

namespace LeavePlanner.Application.Services;

public sealed class LeavePlanningService(
    IHolidaySource holidaySource,
    ISubdivisionCatalog subdivisionCatalog,
    LeavePlannerOptimizer optimizer,
    RecommendationStrategyRegistry strategies)
{
    public async Task<PlanningResult> RecommendAsync(PlanningQuery query, CancellationToken cancellationToken)
    {
        var availableCountries = await holidaySource.GetAvailableCountryCodesAsync(cancellationToken);
        if (!availableCountries.Contains(query.CountryCode))
            throw new InvalidPlanningRequestException("countryCode", "This country is not supported by the holiday provider. Choose a supported ISO alpha-2 code.");

        if (query.SubdivisionCode is { } subdivisionCode &&
            !subdivisionCatalog.Contains(query.CountryCode, subdivisionCode))
            throw new InvalidPlanningRequestException("subdivisionCode", "Use a valid first-level ISO 3166-2 subdivision code belonging to the selected country.");

        var records = new List<HolidayRecord>();
        for (var year = query.FromDate.Year; year <= query.ToDate.Year; year++)
        {
            var yearlyRecords = await holidaySource.GetHolidaysAsync(query.CountryCode, year, cancellationToken);
            records.AddRange(yearlyRecords);
        }

        var holidays = HolidayCalendarNormalizer.Normalize(records, query.CountryCode,
            query.SubdivisionCode, query.FromDate, query.ToDate);
        var strategy = strategies.Get(query.Priority);
        var selection = optimizer.FindBest(query.FromDate, query.ToDate, query.LeaveBudget,
            query.MinTotalDays, holidays, strategy);

        if (selection is null)
        {
            return new PlanningResult(query, null, [],
                "No continuous break meets the leave budget and minimum length. Increase leaveBudget or adjust the dates or minTotalDays within the supported range.");
        }

        var inBreak = holidays
            .Where(holiday => holiday.Date >= selection.Candidate.StartDate && holiday.Date <= selection.Candidate.EndDate)
            .ToArray();
        return new PlanningResult(query, selection, inBreak,
            RecommendationExplanationBuilder.Build(query, selection, strategy));
    }
}
