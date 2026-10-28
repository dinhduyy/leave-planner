using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Common;
using LeavePlanner.Application.Constants;
using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Helpers;
using LeavePlanner.Application.Models;
using LeavePlanner.Domain.Abstractions;
using LeavePlanner.Domain.Models;

namespace LeavePlanner.Application.Services;

public sealed class LeavePlanningService(
    IHolidaySource holidaySource,
    ISubdivisionCatalog subdivisionCatalog,
    RecommendationStrategyRegistry strategies)
{
    public async Task<Result<PlanningResult, PlanningError>> RecommendAsync(PlanningQuery query, CancellationToken cancellationToken)
    {
        var availableCountries = await holidaySource.GetAvailableCountryCodesAsync(cancellationToken);

        if (!availableCountries.Contains(query.CountryCode))
        {
            return new Result<PlanningResult, PlanningError>.Failure(new PlanningValidationError(
                PlanningErrorCodes.UnsupportedCountry, nameof(PlanningQuery.CountryCode),
                "This country is not supported by the holiday provider. Choose a supported ISO alpha-2 code."));
        }

        if (query.SubdivisionCode is { } subdivisionCode &&
            !subdivisionCatalog.Contains(query.CountryCode, subdivisionCode))
        {
            return new Result<PlanningResult, PlanningError>.Failure(new PlanningValidationError(
                PlanningErrorCodes.InvalidSubdivision, nameof(PlanningQuery.SubdivisionCode),
                "Use a valid first-level ISO 3166-2 subdivision code belonging to the selected country."));
        }

        var records = new List<HolidayRecord>();
        for (var year = query.FromDate.Year; year <= query.ToDate.Year; year++)
        {
            var yearlyRecords = await holidaySource.GetHolidaysAsync(query.CountryCode, year, cancellationToken);
            records.AddRange(yearlyRecords);
        }

        var holidays = HolidayCalendarNormalizer.Normalize(records, query.CountryCode,
            query.SubdivisionCode, query.FromDate, query.ToDate);

        var strategy = strategies.Get(query.Priority);

        var selection = FindBest(query.FromDate,
            query.ToDate,
            query.LeaveBudget,
            query.MinTotalDays,
            holidays,
            strategy);

        if (selection is null)
        {
            return new Result<PlanningResult, PlanningError>.Success(new PlanningResult(query, null, [],
                "No continuous break meets the leave budget and minimum length. Increase leaveBudget or adjust the dates or minTotalDays within the supported range."));
        }

        var holidaysInRecommendation = holidays
            .Where(holiday => holiday.Date >= selection.Candidate.StartDate && holiday.Date <= selection.Candidate.EndDate)
            .ToArray();

        return new Result<PlanningResult, PlanningError>.Success(new PlanningResult(query, selection, holidaysInRecommendation,
            RecommendationExplanationBuilder.Build(query, selection, strategy)));
    }

    private static LeavePlanSelection? FindBest(
        DateOnly fromDate,
        DateOnly toDate,
        int leaveBudget,
        int minTotalDays,
        IReadOnlyCollection<ApplicableHoliday> holidays,
        IRecommendationStrategy strategy)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The start date must be on or before the end date.", nameof(fromDate));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(leaveBudget);
        ArgumentOutOfRangeException.ThrowIfLessThan(minTotalDays, 1);
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentNullException.ThrowIfNull(strategy);

        var calendar = new LeaveCalendar(fromDate, toDate, holidays);
        if (minTotalDays > calendar.DayCount)
        {
            return null;
        }

        LeaveCandidate? best = null;
        for (var start = 0; start < calendar.DayCount; start++)
        {
            var leaveDaysRequired = 0;
            for (var end = start; end < calendar.DayCount; end++)
            {
                if (calendar.RequiresLeaveAt(end))
                {
                    leaveDaysRequired++;
                }

                if (leaveDaysRequired > leaveBudget)
                {
                    break;
                }

                var totalDays = end - start + 1;
                if (totalDays < minTotalDays)
                {
                    continue;
                }

                var candidate = new LeaveCandidate(
                    calendar.DateAt(start),
                    calendar.DateAt(end),
                    totalDays,
                    leaveDaysRequired);

                if (best is null || strategy.IsBetter(candidate, best))
                {
                    best = candidate;
                }
            }
        }

        if (best is null)
        {
            return null;
        }

        return new LeavePlanSelection(best,
            calendar.GetLeaveDates(best.StartDate, best.EndDate));
    }
}
