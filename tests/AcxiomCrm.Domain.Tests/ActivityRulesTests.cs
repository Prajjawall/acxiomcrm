using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class ActivityRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Subject_is_required(string? subject)
    {
        var errors = ActivityRules.Validate(
            subject: subject,
            description: null,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Subject");
        errors[0].Message.ShouldBe(Messages.Required("Subject"));
        errors[0].Message.ShouldBe("Subject is required.");
    }

    [Fact]
    public void Subject_cannot_exceed_max_length()
    {
        var errors = ActivityRules.Validate(
            subject: new string('s', 201),
            description: null,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Subject");
        errors[0].Message.ShouldBe(Messages.MaxLength("Subject", 200));
        errors[0].Message.ShouldBe("Subject must not exceed 200 characters.");
    }

    [Fact]
    public void Description_cannot_exceed_max_length()
    {
        var errors = ActivityRules.Validate(
            subject: "Valid subject",
            description: new string('d', 2001),
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Description");
        errors[0].Message.ShouldBe(Messages.MaxLength("Description", 2000));
        errors[0].Message.ShouldBe("Description must not exceed 2000 characters.");
    }

    [Fact]
    public void At_least_one_related_record_is_required()
    {
        var errors = ActivityRules.Validate(
            subject: "Call logged",
            description: "Discussion on pricing",
            relatedCount: 0);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Related");
        errors[0].Message.ShouldBe("Select at least one related record.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Valid_activity_passes_validation(int relatedCount)
    {
        var errors = ActivityRules.Validate(
            subject: "Meeting completed",
            description: "Discussed requirements",
            relatedCount: relatedCount);

        errors.ShouldBeEmpty();
    }
}
