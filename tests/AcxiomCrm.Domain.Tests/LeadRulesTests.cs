using AcxiomCrm.Domain.Rules;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class LeadRulesTests
{
    [Fact]
    public void Valid_lead_produces_no_errors()
    {
        var errors = LeadRules.Validate(
            name: "John Doe",
            company: "Acme Corp",
            email: "john@example.com",
            phone: "9876543210",
            expectedValue: 50000m,
            notes: "Potential enterprise deal");

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Missing_both_email_and_phone_returns_error_on_both_fields()
    {
        var errors = LeadRules.Validate(
            name: "John Doe",
            company: "Acme Corp",
            email: null,
            phone: null,
            expectedValue: 50000m,
            notes: null);

        errors.Count(e => e.Message == "Enter an email address or a phone number.").ShouldBe(2);
        errors.ShouldContain(e => e.Field == "Email" && e.Message == "Enter an email address or a phone number.");
        errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter an email address or a phone number.");
    }

    [Fact]
    public void Whitespace_only_email_and_phone_returns_error_on_both_fields()
    {
        var errors = LeadRules.Validate(
            name: "John Doe",
            company: "Acme Corp",
            email: "   ",
            phone: "   ",
            expectedValue: 50000m,
            notes: null);

        errors.ShouldContain(e => e.Field == "Email" && e.Message == "Enter an email address or a phone number.");
        errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter an email address or a phone number.");
    }

    [Fact]
    public void Email_only_is_valid()
    {
        var errors = LeadRules.Validate(
            name: "John Doe",
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: 0m,
            notes: null);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Phone_only_is_valid()
    {
        var errors = LeadRules.Validate(
            name: "John Doe",
            company: null,
            email: null,
            phone: "9876543210",
            expectedValue: 0m,
            notes: null);

        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, "Lead name is required.")]
    [InlineData("", "Lead name is required.")]
    [InlineData("   ", "Lead name is required.")]
    public void Blank_name_is_rejected(string? name, string expectedMessage)
    {
        var errors = LeadRules.Validate(
            name: name,
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: 0m,
            notes: null);

        errors.ShouldContain(e => e.Field == "Name" && e.Message == expectedMessage);
    }

    [Fact]
    public void Name_exceeding_150_chars_is_rejected()
    {
        var longName = new string('A', 151);
        var errors = LeadRules.Validate(
            name: longName,
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: 0m,
            notes: null);

        errors.ShouldContain(e => e.Field == "Name" && e.Message == "Lead name must not exceed 150 characters.");
    }

    [Fact]
    public void Company_exceeding_150_chars_is_rejected()
    {
        var longCompany = new string('A', 151);
        var errors = LeadRules.Validate(
            name: "John",
            company: longCompany,
            email: "john@example.com",
            phone: null,
            expectedValue: 0m,
            notes: null);

        errors.ShouldContain(e => e.Field == "CompanyName" && e.Message == "Company name must not exceed 150 characters.");
    }

    [Fact]
    public void Invalid_email_format_is_rejected()
    {
        var errors = LeadRules.Validate(
            name: "John",
            company: null,
            email: "invalid-email",
            phone: null,
            expectedValue: 0m,
            notes: null);

        errors.ShouldContain(e => e.Field == "Email" && e.Message == "Enter a valid email address.");
    }

    [Fact]
    public void Invalid_phone_format_is_rejected()
    {
        var errors = LeadRules.Validate(
            name: "John",
            company: null,
            email: null,
            phone: "12345",
            expectedValue: 0m,
            notes: null);

        errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter a valid phone number.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(100000000.01)]
    public void Expected_value_out_of_bounds_is_rejected(decimal val)
    {
        var errors = LeadRules.Validate(
            name: "John",
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: val,
            notes: null);

        errors.ShouldContain(e => e.Field == "ExpectedValue" && e.Message == "Expected value must be between 0 and 10,00,00,000.");
    }

    [Fact]
    public void Expected_value_more_than_two_decimal_places_is_rejected()
    {
        var errors = LeadRules.Validate(
            name: "John",
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: 10.005m,
            notes: null);

        errors.ShouldContain(e => e.Field == "ExpectedValue" && e.Message == "Expected value cannot have more than 2 decimal places.");
    }

    [Fact]
    public void Notes_exceeding_2000_chars_is_rejected()
    {
        var longNotes = new string('N', 2001);
        var errors = LeadRules.Validate(
            name: "John",
            company: null,
            email: "john@example.com",
            phone: null,
            expectedValue: 0m,
            notes: longNotes);

        errors.ShouldContain(e => e.Field == "Notes" && e.Message == "Notes must not exceed 2000 characters.");
    }
}
