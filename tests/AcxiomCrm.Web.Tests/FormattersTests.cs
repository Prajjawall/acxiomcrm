using AcxiomCrm.TestSupport;
using AcxiomCrm.Web.Presentation;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class FormattersTests
{
    [Theory]
    [InlineData(1250000, "₹12,50,000")]
    [InlineData(99999, "₹99,999")]
    [InlineData(1234.50, "₹1,234.50")]
    [InlineData(0, "₹0")]
    [InlineData(-500, "-₹500")]
    public void Money_formats_indian_numbering(decimal amount, string expected)
    {
        var result = Formatters.Money(amount);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1250000, "₹12.5L")]
    [InlineData(32000000, "₹3.2Cr")]
    [InlineData(45000, "₹45K")]
    [InlineData(999, "₹999")]
    [InlineData(-100000, "-₹1L")]
    public void MoneyCompact_formats_lakhs_and_crores(decimal amount, string expected)
    {
        var result = Formatters.MoneyCompact(amount);
        result.ShouldBe(expected);
    }

    [Fact]
    public void Date_formats_standard_short_date()
    {
        var date = new DateOnly(2026, 10, 16);
        var result = Formatters.Date(date);
        result.ShouldBe("16 Oct 2026");
    }

    [Fact]
    public void DateTime_formats_business_local_time()
    {
        var clock = new FakeClock(); // Default UTC+5:30 Asia/Kolkata
        var utc = new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC = 15:30 IST
        var result = Formatters.DateTime(utc, clock);
        result.ShouldBe("16 oct 2026, 3:30 pm");
    }
}
