using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class OpportunityRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.004, false)]
    [InlineData(0.01, true)]
    [InlineData(50000, true)]
    [InlineData(1_000_000_000, true)]
    [InlineData(1_000_000_000.01, false)]
    public void Amount_validation_table(decimal amount, bool isValid)
    {
        var errors = OpportunityRules.Validate(
            name: "Deal Alpha",
            amount: amount,
            probability: 50,
            expectedClose: Today.AddDays(10),
            stage: OpportunityStage.Qualification,
            today: Today,
            notes: null,
            closeNote: null);

        if (isValid)
        {
            errors.ShouldNotContain(e => e.Field == "Amount");
        }
        else
        {
            errors.ShouldContain(e => e.Field == "Amount");
            if (amount <= 0 || amount == 0.004m)
            {
                errors.ShouldContain(e => e.Field == "Amount" && e.Message == Messages.AmountPositive);
            }
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Probability_validation_table(int probability, bool isValid)
    {
        var errors = OpportunityRules.Validate(
            name: "Deal Alpha",
            amount: 1000m,
            probability: probability,
            expectedClose: Today.AddDays(10),
            stage: OpportunityStage.Qualification,
            today: Today,
            notes: null,
            closeNote: null);

        if (isValid)
        {
            errors.ShouldNotContain(e => e.Field == "Probability");
        }
        else
        {
            errors.ShouldContain(e => e.Field == "Probability" && e.Message == Messages.ProbabilityRange);
        }
    }

    [Fact]
    public void ExpectedCloseDate_table()
    {
        // Yesterday -> invalid
        var errorsPast = OpportunityRules.Validate(
            name: "Deal Alpha",
            amount: 1000m,
            probability: 50,
            expectedClose: Today.AddDays(-1),
            stage: OpportunityStage.Qualification,
            today: Today,
            notes: null,
            closeNote: null);
        errorsPast.ShouldContain(e => e.Field == "ExpectedCloseDate" && e.Message == Messages.ExpectedCloseDatePast);

        // Today -> valid
        var errorsToday = OpportunityRules.Validate(
            name: "Deal Alpha",
            amount: 1000m,
            probability: 50,
            expectedClose: Today,
            stage: OpportunityStage.Qualification,
            today: Today,
            notes: null,
            closeNote: null);
        errorsToday.ShouldNotContain(e => e.Field == "ExpectedCloseDate");

        // Tomorrow -> valid
        var errorsTomorrow = OpportunityRules.Validate(
            name: "Deal Alpha",
            amount: 1000m,
            probability: 50,
            expectedClose: Today.AddDays(1),
            stage: OpportunityStage.Qualification,
            today: Today,
            notes: null,
            closeNote: null);
        errorsTomorrow.ShouldNotContain(e => e.Field == "ExpectedCloseDate");
    }

    [Fact]
    public void Closed_stage_skips_amount_and_date_open_rules()
    {
        var errorsWon = OpportunityRules.Validate(
            name: "Deal Won",
            amount: 0m,
            probability: 100,
            expectedClose: Today.AddDays(-5),
            stage: OpportunityStage.Won,
            today: Today,
            notes: null,
            closeNote: "Customer signed annual contract.");

        errorsWon.ShouldNotContain(e => e.Field == "Amount");
        errorsWon.ShouldNotContain(e => e.Field == "ExpectedCloseDate");

        var errorsLost = OpportunityRules.Validate(
            name: "Deal Lost",
            amount: 0m,
            probability: 0,
            expectedClose: Today.AddDays(-5),
            stage: OpportunityStage.Lost,
            today: Today,
            notes: null,
            closeNote: "Budget cuts cancelled project.");

        errorsLost.ShouldNotContain(e => e.Field == "Amount");
        errorsLost.ShouldNotContain(e => e.Field == "ExpectedCloseDate");
    }

    [Theory]
    [InlineData(1250000, 40, 500000.00)]
    [InlineData(33.33, 33, 11.00)]
    [InlineData(0.01, 50, 0.01)]
    public void Weighted_calculation_rounds_half_away_from_zero_to_two_decimals(decimal amount, int probability, decimal expectedWeighted)
    {
        var weighted = OpportunityRules.Weighted(amount, probability);
        weighted.ShouldBe(expectedWeighted);
    }

    [Theory]
    [InlineData(OpportunityStage.Qualification, OpportunityStatus.Open)]
    [InlineData(OpportunityStage.Proposal, OpportunityStatus.Open)]
    [InlineData(OpportunityStage.Negotiation, OpportunityStatus.Open)]
    [InlineData(OpportunityStage.Won, OpportunityStatus.Won)]
    [InlineData(OpportunityStage.Lost, OpportunityStatus.Lost)]
    public void StatusFor_maps_all_stages(OpportunityStage stage, OpportunityStatus expectedStatus)
    {
        OpportunityRules.StatusFor(stage).ShouldBe(expectedStatus);
    }

    [Theory]
    [InlineData(OpportunityStage.Qualification, 20)]
    [InlineData(OpportunityStage.Proposal, 50)]
    [InlineData(OpportunityStage.Negotiation, 75)]
    [InlineData(OpportunityStage.Won, 100)]
    [InlineData(OpportunityStage.Lost, 0)]
    public void DefaultProbability_returns_expected_values(OpportunityStage stage, byte expectedProbability)
    {
        OpportunityRules.DefaultProbability(stage).ShouldBe(expectedProbability);
    }
}
