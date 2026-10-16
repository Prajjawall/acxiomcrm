using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class MessagesTests
{
    [Fact]
    public void Canonical_strings_match_PRD()
    {
        Messages.AmountPositive.ShouldBe("Opportunity Amount must be greater than 0.");
        Messages.ProbabilityRange.ShouldBe("Probability must be between 0 and 100.");
        Messages.ExpectedCloseDatePast.ShouldBe("Expected Close Date cannot be in the past.");
        Messages.FollowUpDateBeforeToday.ShouldBe("Follow-up date cannot be earlier than today.");
        Messages.InvalidEmail.ShouldBe("Enter a valid email address.");
        Messages.InvalidPhone.ShouldBe("Enter a valid phone number.");
        Messages.DuplicateEmail.ShouldBe("A customer with this email already exists.");
        Messages.DuplicatePhone.ShouldBe("A customer with this phone number already exists.");

        Messages.Required("Customer name").ShouldBe("Customer name is required.");
        Messages.MaxLength("Notes", 2000).ShouldBe("Notes must not exceed 2000 characters.");
        Messages.InvalidLeadTransition(LeadStatus.New, LeadStatus.Converted)
            .ShouldBe("A lead cannot move from New to Converted.");
    }
}
