using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZoneInfo;

    public SystemClock(IOptions<BusinessOptions> options, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        var zoneId = options.Value.TimeZone;
        try
        {
            _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"Time zone '{zoneId}' is not recognised by the operating system.", ex);
        }
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZoneInfo);
            return DateOnly.FromDateTime(local.DateTime);
        }
    }

    public DateTime ToBusinessLocal(DateTime utc)
    {
        var utcDateTime = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _timeZoneInfo);
    }
}
