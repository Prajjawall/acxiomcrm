using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class FieldValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Required_blank_and_whitespace_fail_with_label_message(string? value)
    {
        var validator = new FieldValidator();
        validator.Required("Name", "Customer name", value);

        validator.IsValid.ShouldBeFalse();
        validator.Errors.Count.ShouldBe(1);
        validator.Errors[0].Field.ShouldBe("Name");
        validator.Errors[0].Message.ShouldBe("Customer name is required.");
    }

    [Fact]
    public void MaxLength_boundary_150_ok_151_fails()
    {
        var valid = new string('a', 150);
        var invalid = new string('a', 151);

        var v1 = new FieldValidator().MaxLength("Name", "Customer name", valid, 150);
        v1.IsValid.ShouldBeTrue();

        var v2 = new FieldValidator().MaxLength("Name", "Customer name", invalid, 150);
        v2.IsValid.ShouldBeFalse();
        v2.Errors[0].Message.ShouldBe("Customer name must not exceed 150 characters.");
    }

    [Fact]
    public void Email_and_Phone_use_canonical_messages()
    {
        var v = new FieldValidator()
            .Email("Email", "invalid-email")
            .Phone("Phone", "12345");

        v.IsValid.ShouldBeFalse();
        v.Errors.Count.ShouldBe(2);
        v.Errors[0].Field.ShouldBe("Email");
        v.Errors[0].Message.ShouldBe("Enter a valid email address.");
        v.Errors[1].Field.ShouldBe("Phone");
        v.Errors[1].Message.ShouldBe("Enter a valid phone number.");
    }

    [Fact]
    public void One_error_per_field_first_wins()
    {
        var v = new FieldValidator()
            .Required("Name", "Customer name", "")
            .MaxLength("Name", "Customer name", new string('x', 200), 150);

        v.Errors.Count.ShouldBe(1);
        v.Errors[0].Message.ShouldBe("Customer name is required.");
    }

    [Fact]
    public void Optional_email_blank_is_valid()
    {
        var v = new FieldValidator()
            .Email("Email", "", required: false)
            .Email("Email2", null, required: false);

        v.IsValid.ShouldBeTrue();
        v.Errors.ShouldBeEmpty();
    }
}
