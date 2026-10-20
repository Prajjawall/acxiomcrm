using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Application.Reporting;

public static class DateRangeResolver
{
    public static Result<DateRange> Resolve(DatePreset preset, DateOnly? from, DateOnly? to, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var today = clock.Today;

        switch (preset)
        {
            case DatePreset.Today:
                return Result<DateRange>.Ok(new DateRange(today, today));

            case DatePreset.Week:
                var dayOfWeek = (int)today.DayOfWeek;
                var daysSinceMonday = (dayOfWeek == 0 ? 7 : dayOfWeek) - 1;
                var monday = today.AddDays(-daysSinceMonday);
                var sunday = monday.AddDays(6);
                return Result<DateRange>.Ok(new DateRange(monday, sunday));

            case DatePreset.Month:
                var firstDay = new DateOnly(today.Year, today.Month, 1);
                var lastDay = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                return Result<DateRange>.Ok(new DateRange(firstDay, lastDay));

            case DatePreset.All:
                return Result<DateRange>.Ok(new DateRange(null, null));

            case DatePreset.Custom:
                if (from.HasValue && to.HasValue && from.Value > to.Value)
                {
                    return Result<DateRange>.Fail(
                        ErrorKind.Validation,
                        new FieldError("From", "Start date must not be after end date."));
                }

                if (from.HasValue && to.HasValue && (to.Value.DayNumber - from.Value.DayNumber > 365 * 5))
                {
                    return Result<DateRange>.Fail(
                        ErrorKind.Validation,
                        new FieldError("Range", "Choose a range of 5 years or less."));
                }

                return Result<DateRange>.Ok(new DateRange(from, to));

            default:
                return Result<DateRange>.Ok(new DateRange(null, null));
        }
    }
}
