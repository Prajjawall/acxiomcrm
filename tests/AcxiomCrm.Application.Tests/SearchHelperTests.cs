using AcxiomCrm.Application.Common;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class SearchHelperTests
{
    [Fact]
    public void EscapeLike_escapes_special_characters()
    {
        var input = @"50%_[a]\";
        var expected = @"50\%\_\[a]\\";

        SearchHelper.EscapeLike(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalise_blank_or_whitespace_returns_null(string? input)
    {
        SearchHelper.Normalise(input).ShouldBeNull();
    }

    [Fact]
    public void Normalise_trims_and_truncates_to_100()
    {
        var longStr = "   " + new string('x', 120) + "   ";
        var result = SearchHelper.Normalise(longStr);

        result.ShouldNotBeNull();
        result.Length.ShouldBe(100);
        result.ShouldBe(new string('x', 100));
    }
}
