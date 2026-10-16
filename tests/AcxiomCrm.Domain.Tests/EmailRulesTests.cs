using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class EmailRulesTests
{
    [Theory]
    [InlineData("asha@acme.in", true)]
    [InlineData("  asha@acme.in  ", true)]
    [InlineData("user.name+tag@domain.co.in", true)]
    [InlineData("Asha@Acme.IN", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a@b", false)] // no TLD
    [InlineData("a@b.c", false)] // TLD < 2 chars
    [InlineData("@domain.com", false)] // missing local part
    [InlineData("user@", false)] // missing domain
    [InlineData("user@.com", false)] // domain starts with dot
    [InlineData("user..name@domain.com", false)] // double dot in local part
    [InlineData(".user@domain.com", false)] // leading dot in local part
    [InlineData("user.@domain.com", false)] // trailing dot in local part
    public void IsValid_cases(string? email, bool expected)
    {
        var result = EmailRules.IsValid(email);
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsValid_rejects_over_max_length()
    {
        // 255 characters or more must be rejected (Limits.Email = 254)
        var longEmail = new string('a', 245) + "@domain.com"; // 245 + 11 = 256
        EmailRules.IsValid(longEmail).ShouldBeFalse();
    }

    [Fact]
    public void Normalize_trims_and_lowercases()
    {
        EmailRules.Normalize("  Asha@Acme.IN ").ShouldBe("asha@acme.in");
    }
}
