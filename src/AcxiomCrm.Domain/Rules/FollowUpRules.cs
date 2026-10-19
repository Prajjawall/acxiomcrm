using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Rules;

public static class FollowUpRules
{
    public static IReadOnlyList<FieldError> Validate(
        string? subject,
        string? remarks,
        DateOnly followUpBusinessDate,
        DateOnly today,
        bool isHistorical,
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

        if (remarks is not null && remarks.Length > Limits.Notes)
        {
            errors.Add(new FieldError("Remarks", Messages.MaxLength("Remarks", Limits.Notes)));
        }

        if (!isHistorical && followUpBusinessDate < today)
        {
            errors.Add(new FieldError("FollowUpDate", Messages.FollowUpDateBeforeToday));
        }

        if (relatedCount != 1)
        {
            errors.Add(new FieldError("Related", "Choose exactly one related record."));
        }

        return errors;
    }

    public static bool IsOverdue(FollowUpStatus status, DateTime followUpUtc, DateTime nowUtc)
    {
        return status == FollowUpStatus.Planned && followUpUtc < nowUtc;
    }

    public static bool IsDueToday(FollowUpStatus status, DateOnly followUpBusinessDate, DateOnly today)
    {
        return status == FollowUpStatus.Planned && followUpBusinessDate == today;
    }
}
