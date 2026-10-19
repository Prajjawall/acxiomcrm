using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class FollowUpEntityTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_initializes_with_planned_status_and_zero_reschedules()
    {
        var followUp = FollowUp.Create(
            customerId: 1,
            leadId: null,
            opportunityId: null,
            followUpDate: FixedDate,
            subject: "Call client",
            type: FollowUpType.Call,
            remarks: "Initial call",
            assignedToId: "user-1",
            isHistoricalEntry: false);

        followUp.Status.ShouldBe(FollowUpStatus.Planned);
        followUp.RescheduleCount.ShouldBe(0);
        followUp.CompletedDate.ShouldBeNull();
        followUp.RelatedType.ShouldBe(RelatedType.Customer);
        followUp.OwnerId.ShouldBe("user-1");
    }

    [Fact]
    public void RelatedType_derives_correctly()
    {
        var custFollowUp = FollowUp.Create(1, null, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);
        custFollowUp.RelatedType.ShouldBe(RelatedType.Customer);

        var leadFollowUp = FollowUp.Create(null, 2, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);
        leadFollowUp.RelatedType.ShouldBe(RelatedType.Lead);

        var oppFollowUp = FollowUp.Create(null, null, 3, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);
        oppFollowUp.RelatedType.ShouldBe(RelatedType.Opportunity);
    }

    [Fact]
    public void Complete_transitions_from_planned_to_completed()
    {
        var followUp = FollowUp.Create(1, null, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);
        var completedAt = FixedDate.AddHours(2);

        followUp.Complete("Spoke with client, agreed on next steps.", completedAt);

        followUp.Status.ShouldBe(FollowUpStatus.Completed);
        followUp.CompletedDate.ShouldBe(completedAt);
        followUp.Remarks.ShouldBe("Spoke with client, agreed on next steps.");

        // Completing again throws InvalidOperationException
        Should.Throw<InvalidOperationException>(() => followUp.Complete("Again", completedAt));
    }

    [Fact]
    public void Reschedule_increments_reschedule_count_and_updates_date()
    {
        var followUp = FollowUp.Create(1, null, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);
        var newDate = FixedDate.AddDays(2);

        followUp.Reschedule(newDate);

        followUp.Status.ShouldBe(FollowUpStatus.Planned);
        followUp.RescheduleCount.ShouldBe(1);
        followUp.FollowUpDate.ShouldBe(newDate);

        var newDate2 = FixedDate.AddDays(4);
        followUp.Reschedule(newDate2);
        followUp.RescheduleCount.ShouldBe(2);
        followUp.FollowUpDate.ShouldBe(newDate2);
    }

    [Fact]
    public void MarkMissed_transitions_from_planned_to_missed()
    {
        var followUp = FollowUp.Create(1, null, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);

        followUp.MarkMissed();

        followUp.Status.ShouldBe(FollowUpStatus.Missed);
        Should.Throw<InvalidOperationException>(() => followUp.MarkMissed());
        Should.Throw<InvalidOperationException>(() => followUp.Reschedule(FixedDate.AddDays(1)));
        Should.Throw<InvalidOperationException>(() => followUp.Complete(null, FixedDate));
        Should.Throw<InvalidOperationException>(() => followUp.Cancel());
    }

    [Fact]
    public void Cancel_transitions_from_planned_to_cancelled()
    {
        var followUp = FollowUp.Create(1, null, null, FixedDate, "Call", FollowUpType.Call, null, "user-1", false);

        followUp.Cancel();

        followUp.Status.ShouldBe(FollowUpStatus.Cancelled);
        Should.Throw<InvalidOperationException>(() => followUp.Cancel());
        Should.Throw<InvalidOperationException>(() => followUp.Complete(null, FixedDate));
        Should.Throw<InvalidOperationException>(() => followUp.Reschedule(FixedDate.AddDays(1)));
        Should.Throw<InvalidOperationException>(() => followUp.MarkMissed());
    }
}
