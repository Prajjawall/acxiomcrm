using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.TestSupport;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _utcNow;
    private readonly TimeZoneInfo _timeZoneInfo;

    public FakeClock() : this(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc))
    {
    }

    public FakeClock(DateTime utcNow, string timeZoneId = "Asia/Kolkata")
    {
        _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTime UtcNow => _utcNow.UtcDateTime;

    public DateOnly Today
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(_utcNow, _timeZoneInfo);
            return DateOnly.FromDateTime(local.DateTime);
        }
    }

    public DateTime ToBusinessLocal(DateTime utc)
    {
        var utcDateTime = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _timeZoneInfo);
    }

    public DateTime ToUtcFromBusinessLocal(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZoneInfo);
    }

    public DateOnly BusinessDateOf(DateTime utc)
    {
        var local = ToBusinessLocal(utc);
        return DateOnly.FromDateTime(local);
    }

    public void Set(DateTime utcNow)
    {
        _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    public void Advance(TimeSpan by)
    {
        _utcNow = _utcNow.Add(by);
    }
}
