using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class OpportunityEntityTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Fact]
    public void MoveTo_Won_sets_probability_100_closeddate_and_status()
    {
        var opp = Opportunity.Create(
            name: "Enterprise Upgrade",
            customerId: 1,
            amount: 50000m,
            stage: OpportunityStage.Qualification,
            probability: 20,
            expectedCloseDate: Today.AddDays(30),
            ownerId: "user-1");

        opp.MoveTo(OpportunityStage.Won, Today, "Signed multi-year agreement");

        opp.Stage.ShouldBe(OpportunityStage.Won);
        opp.Status.ShouldBe(OpportunityStatus.Won);
        opp.Probability.ShouldBe((byte)100);
        opp.ClosedDate.ShouldBe(Today);
        opp.CloseNote.ShouldBe("Signed multi-year agreement");
    }

    [Fact]
    public void MoveTo_Lost_sets_0()
    {
        var opp = Opportunity.Create(
            name: "Cloud Migration",
            customerId: 1,
            amount: 25000m,
            stage: OpportunityStage.Proposal,
            probability: 50,
            expectedCloseDate: Today.AddDays(15),
            ownerId: "user-1");

        opp.MoveTo(OpportunityStage.Lost, Today, "Competitor price undercut");

        opp.Stage.ShouldBe(OpportunityStage.Lost);
        opp.Status.ShouldBe(OpportunityStatus.Lost);
        opp.Probability.ShouldBe((byte)0);
        opp.ClosedDate.ShouldBe(Today);
        opp.CloseNote.ShouldBe("Competitor price undercut");
    }

    [Fact]
    public void Won_requires_note()
    {
        var opp = Opportunity.Create(
            name: "Security Suite",
            customerId: 1,
            amount: 10000m,
            stage: OpportunityStage.Negotiation,
            probability: 75,
            expectedCloseDate: Today.AddDays(5),
            ownerId: "user-1");

        Should.Throw<InvalidOperationException>(() => opp.MoveTo(OpportunityStage.Won, Today, null!));
        Should.Throw<InvalidOperationException>(() => opp.MoveTo(OpportunityStage.Won, Today, "   "));
    }

    [Fact]
    public void MoveTo_from_closed_throws()
    {
        var opp = Opportunity.Create(
            name: "ERP License",
            customerId: 1,
            amount: 10000m,
            stage: OpportunityStage.Qualification,
            probability: 20,
            expectedCloseDate: Today.AddDays(5),
            ownerId: "user-1");

        opp.MoveTo(OpportunityStage.Won, Today, "Won deal");

        Should.Throw<InvalidOperationException>(() => opp.MoveTo(OpportunityStage.Negotiation, Today, null));
    }

    [Fact]
    public void Reopen_clears_close_fields()
    {
        var opp = Opportunity.Create(
            name: "Analytics Module",
            customerId: 1,
            amount: 40000m,
            stage: OpportunityStage.Qualification,
            probability: 20,
            expectedCloseDate: Today.AddDays(10),
            ownerId: "user-1");

        opp.MoveTo(OpportunityStage.Lost, Today, "Client postponed");
        opp.Status.ShouldBe(OpportunityStatus.Lost);

        opp.Reopen();

        opp.Stage.ShouldBe(OpportunityStage.Negotiation);
        opp.Status.ShouldBe(OpportunityStatus.Open);
        opp.Probability.ShouldBe((byte)75);
        opp.ClosedDate.ShouldBeNull();
        opp.CloseNote.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_on_closed_throws()
    {
        var opp = Opportunity.Create(
            name: "Support Contract",
            customerId: 1,
            amount: 15000m,
            stage: OpportunityStage.Qualification,
            probability: 20,
            expectedCloseDate: Today.AddDays(10),
            ownerId: "user-1");

        opp.MoveTo(OpportunityStage.Won, Today, "Customer accepted terms");

        Should.Throw<InvalidOperationException>(() =>
            opp.UpdateDetails("Support Contract V2", 20000m, Today.AddDays(20), 50, LeadSource.Website, null));
    }
}
