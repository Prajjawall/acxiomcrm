using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Reporting;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class DateRangeResolverTests
{
    private readonly IClock _clock = Substitute.For<IClock>();

    public DateRangeResolverTests()
    {
        // Friday 16 October 2026
        _clock.Today.Returns(new DateOnly(2026, 10, 16));
        _clock.ToUtcFromBusinessLocal(Arg.Any<DateTime>()).Returns(ci => ci.Arg<DateTime>());
    }

    [Fact]
    public void Resolve_Today_returns_today_for_both_from_and_to()
    {
        var result = DateRangeResolver.Resolve(DatePreset.Today, null, null, _clock);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.From.ShouldBe(new DateOnly(2026, 10, 16));
        result.Value.To.ShouldBe(new DateOnly(2026, 10, 16));
    }

    [Fact]
    public void Resolve_Week_returns_monday_to_sunday()
    {
        var result = DateRangeResolver.Resolve(DatePreset.Week, null, null, _clock);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.From.ShouldBe(new DateOnly(2026, 10, 12)); // Monday
        result.Value.To.ShouldBe(new DateOnly(2026, 10, 18));   // Sunday
    }

    [Fact]
    public void Resolve_Month_returns_first_to_last_day_of_month()
    {
        var result = DateRangeResolver.Resolve(DatePreset.Month, null, null, _clock);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.From.ShouldBe(new DateOnly(2026, 10, 1));
        result.Value.To.ShouldBe(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void Resolve_All_returns_null_range()
    {
        var result = DateRangeResolver.Resolve(DatePreset.All, null, null, _clock);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.From.ShouldBeNull();
        result.Value.To.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Custom_with_from_after_to_fails()
    {
        var from = new DateOnly(2026, 10, 20);
        var to = new DateOnly(2026, 10, 10);

        var result = DateRangeResolver.Resolve(DatePreset.Custom, from, to, _clock);

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Message == "Start date must not be after end date.");
    }

    [Fact]
    public void Resolve_Custom_with_more_than_5_years_fails()
    {
        var from = new DateOnly(2020, 1, 1);
        var to = new DateOnly(2026, 1, 2);

        var result = DateRangeResolver.Resolve(DatePreset.Custom, from, to, _clock);

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Message == "Choose a range of 5 years or less.");
    }
}
