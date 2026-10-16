using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_has_no_errors()
    {
        var result = Result.Ok();

        result.Succeeded.ShouldBeTrue();
        result.Kind.ShouldBe(ErrorKind.None);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Fail_carries_kind_and_errors()
    {
        var error = new FieldError("Name", "Name is required.");
        var result = Result.Fail(ErrorKind.Validation, error);

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Field.ShouldBe("Name");
        result.Errors[0].Message.ShouldBe("Name is required.");
    }

    [Fact]
    public void Fail_with_None_kind_throws()
    {
        Should.Throw<ArgumentException>(() => Result.Fail(ErrorKind.None));
    }

    [Fact]
    public void ResultOfT_Ok_exposes_value()
    {
        var result = Result<string>.Ok("success-value");

        result.Succeeded.ShouldBeTrue();
        result.Kind.ShouldBe(ErrorKind.None);
        result.Value.ShouldBe("success-value");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ResultOfT_Fail_value_is_default()
    {
        var result = Result<string>.Fail(ErrorKind.NotFound, new FieldError("Id", "Not found"));

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.NotFound);
        result.Value.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
    }
}
