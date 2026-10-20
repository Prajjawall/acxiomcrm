using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Application.Reporting;

public sealed record DateRange(DateOnly? From, DateOnly? To)
{
    public DateTime? FromUtc(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!From.HasValue)
        {
            return null;
        }

        var localMidnight = From.Value.ToDateTime(TimeOnly.MinValue);
        return clock.ToUtcFromBusinessLocal(localMidnight);
    }

    public DateTime? ToExclusiveUtc(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!To.HasValue)
        {
            return null;
        }

        var nextDayLocalMidnight = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return clock.ToUtcFromBusinessLocal(nextDayLocalMidnight);
    }
}
