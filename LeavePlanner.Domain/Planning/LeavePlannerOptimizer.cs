using LeavePlanner.Domain.Calendar;
using LeavePlanner.Domain.Strategies;

namespace LeavePlanner.Domain.Planning;

public sealed class LeavePlannerOptimizer
{
    public LeavePlanSelection? FindBest(
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
