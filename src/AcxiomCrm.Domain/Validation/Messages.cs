using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Domain.Validation;

public static class Messages
{
    public const string AmountPositive = "Opportunity Amount must be greater than 0.";
    public const string ProbabilityRange = "Probability must be between 0 and 100.";
    public const string ExpectedCloseDatePast = "Expected Close Date cannot be in the past.";
    public const string FollowUpDateBeforeToday = "Follow-up date cannot be earlier than today.";
    public const string InvalidEmail = "Enter a valid email address.";
    public const string InvalidPhone = "Enter a valid phone number.";
    public const string DuplicateEmail = "A customer with this email already exists.";
    public const string DuplicatePhone = "A customer with this phone number already exists.";

    public static string Required(string label) => $"{label} is required.";

    public static string MaxLength(string label, int max) => $"{label} must not exceed {max} characters.";

    public static string InvalidLeadTransition(LeadStatus from, LeadStatus to) =>
        $"A lead cannot move from {from} to {to}.";
}
