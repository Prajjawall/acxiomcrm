using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class PhoneRulesTests
{
    [Theory]
    [InlineData("9876543210", "9876543210")]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("098765-43210", "9876543210")]
    [InlineData("91 9876543210", "9876543210")]
    public void TryNormalize_accepts_known_formats(string input, string expected)
    {
        var ok = PhoneRules.TryNormalize(input, out var normalized);

        ok.ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("98765 4321")] // 9 digits
    [InlineData("5876543210")] // starts with 5
    [InlineData("98765432101")] // 11 digits without prefix
    [InlineData("abcdefghij")] // non-digits
    [InlineData("９８７６５４３２１０")] // full-width digits
    public void TryNormalize_rejects_invalid(string? input)
    {
        var ok = PhoneRules.TryNormalize(input, out var normalized);

        ok.ShouldBeFalse();
        normalized.ShouldBe(string.Empty);
    }
}
