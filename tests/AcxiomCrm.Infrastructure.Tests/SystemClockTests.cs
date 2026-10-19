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

    [Fact]
    public void ToUtcFromBusinessLocal_roundtrips_and_handles_DST_zone()
    {
        // 1. Kolkata (no DST)
        var optionsKolkata = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "Asia/Kolkata" });
        var clockKolkata = new SystemClock(optionsKolkata);
        var localKolkata = new DateTime(2026, 10, 16, 17, 30, 0);
        var utcKolkata = clockKolkata.ToUtcFromBusinessLocal(localKolkata);
        utcKolkata.ShouldBe(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));

        // 2. New York (has DST: EDT UTC-4 in summer, EST UTC-5 in winter)
        // July 15 (EDT, UTC-4): 10:00 local -> 14:00 UTC
        var optionsNy = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "America/New_York" });
        var clockNy = new SystemClock(optionsNy);
        var localSummer = new DateTime(2026, 7, 15, 10, 0, 0);
        var utcSummer = clockNy.ToUtcFromBusinessLocal(localSummer);
        utcSummer.ShouldBe(new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc));

        // Dec 15 (EST, UTC-5): 10:00 local -> 15:00 UTC
        var localWinter = new DateTime(2026, 12, 15, 10, 0, 0);
        var utcWinter = clockNy.ToUtcFromBusinessLocal(localWinter);
        utcWinter.ShouldBe(new DateTime(2026, 12, 15, 15, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-10-16T18:29:00Z", 2026, 10, 16)]
    [InlineData("2026-10-16T18:30:00Z", 2026, 10, 17)]
    public void BusinessDateOf_at_boundary(string utcInstant, int year, int month, int day)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "Asia/Kolkata" });
        var clock = new SystemClock(options);
        var utc = DateTime.Parse(utcInstant, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);

        clock.BusinessDateOf(utc).ShouldBe(new DateOnly(year, month, day));
    }
}
