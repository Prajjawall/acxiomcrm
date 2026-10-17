using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Rules;

public static class OpportunityRules
{
    public static decimal Weighted(decimal amount, int probability)
    {
        return Math.Round(amount * probability / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static OpportunityStatus StatusFor(OpportunityStage stage)
    {
        return stage switch
        {
            OpportunityStage.Qualification or OpportunityStage.Proposal or OpportunityStage.Negotiation => OpportunityStatus.Open,
            OpportunityStage.Won => OpportunityStatus.Won,
            OpportunityStage.Lost => OpportunityStatus.Lost,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }

    public static byte DefaultProbability(OpportunityStage stage)
    {
        return stage switch
        {
            OpportunityStage.Qualification => 20,
            OpportunityStage.Proposal => 50,
            OpportunityStage.Negotiation => 75,
            OpportunityStage.Won => 100,
            OpportunityStage.Lost => 0,
            _ => 20
        };
    }

    public static bool IsOpen(OpportunityStage stage)
    {
        return stage is OpportunityStage.Qualification or OpportunityStage.Proposal or OpportunityStage.Negotiation;
    }

    public static IReadOnlyList<FieldError> Validate(
        string? name,
        decimal amount,
        int probability,
        DateOnly? expectedClose,
        OpportunityStage stage,
        DateOnly today,
        string? notes,
        string? closeNote)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new FieldError("Name", Messages.Required("Opportunity name")));
        }
        else if (name.Length > Limits.Name)
        {
            errors.Add(new FieldError("Name", Messages.MaxLength("Opportunity name", Limits.Name)));
        }

        var roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (IsOpen(stage))
        {
            if (roundedAmount <= 0)
            {
                errors.Add(new FieldError("Amount", Messages.AmountPositive));
            }
            else if (roundedAmount > Limits.OpportunityMaxAmount)
            {
                errors.Add(new FieldError("Amount", $"Opportunity Amount must not exceed {Limits.OpportunityMaxAmount:N0}."));
            }
        }
        else
        {
            if (roundedAmount < 0)
            {
                errors.Add(new FieldError("Amount", Messages.AmountPositive));
            }
            else if (roundedAmount > Limits.OpportunityMaxAmount)
            {
                errors.Add(new FieldError("Amount", $"Opportunity Amount must not exceed {Limits.OpportunityMaxAmount:N0}."));
            }
        }

        if (probability is < 0 or > 100)
        {
            errors.Add(new FieldError("Probability", Messages.ProbabilityRange));
        }

        if (!expectedClose.HasValue)
        {
            errors.Add(new FieldError("ExpectedCloseDate", Messages.Required("Expected Close Date")));
        }
        else if (IsOpen(stage) && expectedClose.Value < today)
        {
            errors.Add(new FieldError("ExpectedCloseDate", Messages.ExpectedCloseDatePast));
        }

        if (notes is not null && notes.Length > Limits.Notes)
        {
            errors.Add(new FieldError("Notes", Messages.MaxLength("Notes", Limits.Notes)));
        }

        if (stage == OpportunityStage.Won)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                errors.Add(new FieldError("CloseNote", "Enter a note about why this opportunity was won."));
            }
            else if (closeNote.Length > Limits.CloseReason)
            {
                errors.Add(new FieldError("CloseNote", Messages.MaxLength("Close note", Limits.CloseReason)));
            }
        }
        else if (stage == OpportunityStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                errors.Add(new FieldError("CloseNote", "Enter the reason this opportunity was lost."));
            }
            else if (closeNote.Length > Limits.CloseReason)
            {
                errors.Add(new FieldError("CloseNote", Messages.MaxLength("Close note", Limits.CloseReason)));
            }
        }

        return errors;
    }
}
