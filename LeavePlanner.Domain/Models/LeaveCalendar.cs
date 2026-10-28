namespace LeavePlanner.Domain.Models;

/// <summary>
/// The fixed Monday–Friday work calendar for one inclusive planning range.
/// Applicable public holidays are already filtered by the application layer.
/// </summary>
public sealed class LeaveCalendar
{
    private readonly DateOnly _fromDate;
    private readonly bool[] _requiresLeave;

    public LeaveCalendar(DateOnly fromDate, DateOnly toDate, IReadOnlyCollection<ApplicableHoliday> holidays)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The start date must be on or before the end date.", nameof(fromDate));
        }
        ArgumentNullException.ThrowIfNull(holidays);

        _fromDate = fromDate;
        _requiresLeave = new bool[toDate.DayNumber - fromDate.DayNumber + 1];
        var holidayDays = holidays.Select(holiday => holiday.Date.DayNumber).ToHashSet();

        for (var offset = 0; offset < _requiresLeave.Length; offset++)
        {
            var date = DateAt(offset);
            _requiresLeave[offset] = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
                                     !holidayDays.Contains(date.DayNumber);
        }
    }

    public int DayCount => _requiresLeave.Length;

    public DateOnly DateAt(int offset)
    {
        if ((uint)offset >= _requiresLeave.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        return DateOnly.FromDayNumber(_fromDate.DayNumber + offset);
    }

    public bool RequiresLeaveAt(int offset) => _requiresLeave[offset];

    public IReadOnlyList<DateOnly> GetLeaveDates(DateOnly startDate, DateOnly endDate)
    {
        var startOffset = startDate.DayNumber - _fromDate.DayNumber;
        var endOffset = endDate.DayNumber - _fromDate.DayNumber;
        if (startOffset < 0 || endOffset < startOffset || endOffset >= DayCount)
        {
            throw new ArgumentOutOfRangeException(nameof(startDate), "The break must be within the calendar range.");
        }

        var dates = new List<DateOnly>();
        for (var offset = startOffset; offset <= endOffset; offset++)
        {
            if (_requiresLeave[offset])
            {
                dates.Add(DateAt(offset));
            }
        }

        return dates;
    }
}
