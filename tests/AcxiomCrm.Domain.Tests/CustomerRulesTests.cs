using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class CustomerRulesTests
{
    [Fact]
    public void Valid_input_produces_no_errors()
    {
        var errors = CustomerRules.Validate(
            name: "Acme Corp",
            company: "Acme Industries",
            email: "contact@acme.com",
            phone: "9876543210",
            address: "123 Business Rd",
            city: "Mumbai",
            state: "Maharashtra",
            notes: "Important client");

        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Whitespace_name_is_required(string? name)
    {
        var errors = CustomerRules.Validate(
            name: name,
            company: null,
            email: "contact@acme.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);

        errors.ShouldContain(e => e.Field == "Name" && e.Message == "Customer name is required.");
    }

    [Fact]
    public void Name_length_boundary_150_ok_151_fails()
    {
        var ok = CustomerRules.Validate(
            name: new string('a', Limits.Name),
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        ok.ShouldBeEmpty();

        var fails = CustomerRules.Validate(
            name: new string('a', Limits.Name + 1),
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        fails.ShouldContain(e => e.Field == "Name" && e.Message == "Customer name must not exceed 150 characters.");
    }

    [Fact]
    public void Company_length_boundary_150_ok_151_fails()
    {
        var ok = CustomerRules.Validate(
            name: "Valid Name",
            company: new string('c', Limits.Name),
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        ok.ShouldBeEmpty();

        var fails = CustomerRules.Validate(
            name: "Valid Name",
            company: new string('c', Limits.Name + 1),
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        fails.ShouldContain(e => e.Field == "CompanyName" && e.Message == "Company name must not exceed 150 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@domain.com")]
    public void Email_invalid_or_blank_uses_canonical_message(string? email)
    {
        var errors = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: email,
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);

        errors.ShouldContain(e => e.Field == "Email" && e.Message == Messages.InvalidEmail);
    }

    [Fact]
    public void Email_overflow_boundary_254_ok_255_fails()
    {
        var local = new string('a', 64);
        var d1 = new string('b', 63);
        var d2 = new string('c', 63);
        var d3 = new string('d', 57);
        var okEmail = $"{local}@{d1}.{d2}.{d3}.com"; // 64 + 1 + 63 + 1 + 63 + 1 + 57 + 1 + 3 = 254
        var ok = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: okEmail,
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        ok.ShouldBeEmpty();

        var fails = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: okEmail + "x",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: null);
        fails.ShouldContain(e => e.Field == "Email" && e.Message == "Email must not exceed 254 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("5876543210")]
    [InlineData("abcdefghij")]
    public void Phone_invalid_or_blank_uses_canonical_message(string? phone)
    {
        var errors = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: phone,
            address: null,
            city: null,
            state: null,
            notes: null);

        errors.ShouldContain(e => e.Field == "Phone" && e.Message == Messages.InvalidPhone);
    }

    [Fact]
    public void Address_length_boundary_300_ok_301_fails()
    {
        var ok = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: new string('a', Limits.Address),
            city: null,
            state: null,
            notes: null);
        ok.ShouldBeEmpty();

        var fails = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: new string('a', Limits.Address + 1),
            city: null,
            state: null,
            notes: null);
        fails.ShouldContain(e => e.Field == "Address" && e.Message == "Address must not exceed 300 characters.");
    }

    [Fact]
    public void City_and_State_length_boundary_100_ok_101_fails()
    {
        var fails = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: new string('c', Limits.CityState + 1),
            state: new string('s', Limits.CityState + 1),
            notes: null);

        fails.ShouldContain(e => e.Field == "City" && e.Message == "City must not exceed 100 characters.");
        fails.ShouldContain(e => e.Field == "State" && e.Message == "State must not exceed 100 characters.");
    }

    [Fact]
    public void Notes_length_boundary_2000_ok_2001_fails()
    {
        var ok = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: new string('n', Limits.Notes));
        ok.ShouldBeEmpty();

        var fails = CustomerRules.Validate(
            name: "Valid Name",
            company: null,
            email: "test@example.com",
            phone: "9876543210",
            address: null,
            city: null,
            state: null,
            notes: new string('n', Limits.Notes + 1));
        fails.ShouldContain(e => e.Field == "Notes" && e.Message == "Notes must not exceed 2000 characters.");
    }
}
