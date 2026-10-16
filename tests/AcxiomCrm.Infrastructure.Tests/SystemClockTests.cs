using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Time;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class SystemClockTests
{
    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
    [Theory]
    [InlineData("2026-10-16T18:29:00Z", "Asia/Kolkata", 2026, 10, 16)]
    [InlineData("2026-10-16T18:30:00Z", "Asia/Kolkata", 2026, 10, 17)]
    [InlineData("2026-10-16T18:29:00Z", "UTC", 2026, 10, 16)]
    [InlineData("2026-10-16T18:30:00Z", "UTC", 2026, 10, 16)]
    public void Today_uses_business_zone(string utcInstant, string timeZoneId, int year, int month, int day)
    {
        var instant = DateTimeOffset.Parse(utcInstant, System.Globalization.CultureInfo.InvariantCulture);
        var timeProvider = new TestTimeProvider(instant);
        var options = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = timeZoneId });

        var clock = new SystemClock(options, timeProvider);

        clock.Today.ShouldBe(new DateOnly(year, month, day));
    }

    [Fact]
    public void Unknown_zone_throws_InvalidOperationException_with_zone_id_in_message()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "Invalid/Zone_Id" });

        var ex = Should.Throw<InvalidOperationException>(() => new SystemClock(options));
        ex.Message.ShouldContain("Invalid/Zone_Id");
    }

    [Fact]
    public void ToBusinessLocal_converts()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "Asia/Kolkata" });
        var clock = new SystemClock(options);
        var utc = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        var local = clock.ToBusinessLocal(utc);

        local.ShouldBe(new DateTime(2026, 10, 16, 17, 30, 0));
    }
}
