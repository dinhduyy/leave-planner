using LeavePlanner.Domain.Models;

namespace LeavePlanner.Domain.Tests;

public sealed class LeaveCalendarTests
{
    [Fact]
    public void RequiresLeave_ExcludesWeekendsAndApplicableHolidays()
    {
        var start = new DateOnly(2026, 1, 2); // Friday
        var calendar = new LeaveCalendar(start, start.AddDays(3),
            [new ApplicableHoliday(start, ["Synthetic Friday"]),
             new ApplicableHoliday(start, ["Duplicate"])]);

        Assert.False(calendar.RequiresLeaveAt(0));
        Assert.False(calendar.RequiresLeaveAt(1));
        Assert.False(calendar.RequiresLeaveAt(2));
        Assert.True(calendar.RequiresLeaveAt(3));
        Assert.Equal([start.AddDays(3)], calendar.GetLeaveDates(start, start.AddDays(3)));
    }

    [Fact]
    public void Calendar_HandlesDateOnlyMaximumWithoutAddingPastEnd()
    {
        var calendar = new LeaveCalendar(DateOnly.MaxValue, DateOnly.MaxValue, []);

        Assert.Equal(1, calendar.DayCount);
        Assert.Equal(DateOnly.MaxValue, calendar.DateAt(0));
        Assert.Equal([DateOnly.MaxValue], calendar.GetLeaveDates(DateOnly.MaxValue, DateOnly.MaxValue));
    }
}
