using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Rules;

public static class ActivityRules
{
    public static IReadOnlyList<FieldError> Validate(
        string? subject,
        string? description,
        int relatedCount)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(subject))
        {
            errors.Add(new FieldError("Subject", Messages.Required("Subject")));
        }
        else if (subject.Length > Limits.Subject)
        {
            errors.Add(new FieldError("Subject", Messages.MaxLength("Subject", Limits.Subject)));
        }

        if (description is not null && description.Length > Limits.Notes)
        {
            errors.Add(new FieldError("Description", Messages.MaxLength("Description", Limits.Notes)));
        }

        if (relatedCount < 1)
        {
            errors.Add(new FieldError("Related", "Select at least one related record."));
        }

        return errors;
    }
}
