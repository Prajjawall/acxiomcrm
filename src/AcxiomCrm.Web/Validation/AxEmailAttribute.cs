using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxEmailAttribute : ValidationAttribute
{
    public AxEmailAttribute()
    {
        ErrorMessage = "Enter a valid email address.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return true;
            }

            return EmailRules.IsValid(s);
        }

        return false;
    }
}
