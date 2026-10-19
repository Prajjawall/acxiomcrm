using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class FollowUpRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Fact]
    public void Yesterday_follow_up_date_is_rejected_with_exact_message()
    {
        var errors = FollowUpRules.Validate(
            subject: "Call client",
            remarks: null,
            followUpBusinessDate: Today.AddDays(-1),
            today: Today,
            isHistorical: false,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("FollowUpDate");
        errors[0].Message.ShouldBe(Messages.FollowUpDateBeforeToday);
        errors[0].Message.ShouldBe("Follow-up date cannot be earlier than today.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void Today_and_future_follow_up_date_is_accepted(int daysAhead)
    {
        var errors = FollowUpRules.Validate(
            subject: "Call client",
            remarks: null,
            followUpBusinessDate: Today.AddDays(daysAhead),
            today: Today,
            isHistorical: false,
            relatedCount: 1);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Historical_entry_allows_past_date()
    {
        var errors = FollowUpRules.Validate(
            subject: "Past meeting completed earlier",
            remarks: "Meeting notes",
            followUpBusinessDate: Today.AddDays(-10),
            today: Today,
            isHistorical: true,
            relatedCount: 1);

        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Related_count_must_be_exactly_one(int count)
    {
        var errors = FollowUpRules.Validate(
            subject: "Check in",
            remarks: null,
            followUpBusinessDate: Today,
            today: Today,
            isHistorical: false,
            relatedCount: count);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Related");
        errors[0].Message.ShouldBe("Choose exactly one related record.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Subject_is_required(string? subject)
    {
        var errors = FollowUpRules.Validate(
            subject: subject,
            remarks: null,
            followUpBusinessDate: Today,
            today: Today,
            isHistorical: false,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Subject");
        errors[0].Message.ShouldBe(Messages.Required("Subject"));
        errors[0].Message.ShouldBe("Subject is required.");
    }

    [Fact]
    public void Subject_cannot_exceed_max_length()
    {
        var errors = FollowUpRules.Validate(
            subject: new string('a', 201),
            remarks: null,
            followUpBusinessDate: Today,
            today: Today,
            isHistorical: false,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Subject");
        errors[0].Message.ShouldBe(Messages.MaxLength("Subject", 200));
        errors[0].Message.ShouldBe("Subject must not exceed 200 characters.");
    }

    [Fact]
    public void Remarks_cannot_exceed_max_length()
    {
        var errors = FollowUpRules.Validate(
            subject: "Valid subject",
            remarks: new string('r', 2001),
            followUpBusinessDate: Today,
            today: Today,
            isHistorical: false,
            relatedCount: 1);

        errors.Count.ShouldBe(1);
        errors[0].Field.ShouldBe("Remarks");
        errors[0].Message.ShouldBe(Messages.MaxLength("Remarks", 2000));
        errors[0].Message.ShouldBe("Remarks must not exceed 2000 characters.");
    }

    [Fact]
    public void IsOverdue_truth_table()
    {
        var now = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc);
        var past = now.AddMinutes(-1);
        var future = now.AddMinutes(1);

        // Planned and past is overdue
        FollowUpRules.IsOverdue(FollowUpStatus.Planned, past, now).ShouldBeTrue();
        // Planned and future is not overdue
        FollowUpRules.IsOverdue(FollowUpStatus.Planned, future, now).ShouldBeFalse();

        // Non-planned statuses are NEVER overdue even if date is past
        FollowUpRules.IsOverdue(FollowUpStatus.Completed, past, now).ShouldBeFalse();
        FollowUpRules.IsOverdue(FollowUpStatus.Missed, past, now).ShouldBeFalse();
        FollowUpRules.IsOverdue(FollowUpStatus.Cancelled, past, now).ShouldBeFalse();
    }

    [Fact]
    public void IsDueToday_truth_table()
    {
        // Planned and today is true
        FollowUpRules.IsDueToday(FollowUpStatus.Planned, Today, Today).ShouldBeTrue();
        FollowUpRules.IsDueToday(FollowUpStatus.Planned, Today.AddDays(1), Today).ShouldBeFalse();
        FollowUpRules.IsDueToday(FollowUpStatus.Planned, Today.AddDays(-1), Today).ShouldBeFalse();

        // Completed / Cancelled / Missed are never due today
        FollowUpRules.IsDueToday(FollowUpStatus.Completed, Today, Today).ShouldBeFalse();
        FollowUpRules.IsDueToday(FollowUpStatus.Cancelled, Today, Today).ShouldBeFalse();
        FollowUpRules.IsDueToday(FollowUpStatus.Missed, Today, Today).ShouldBeFalse();
    }
}
