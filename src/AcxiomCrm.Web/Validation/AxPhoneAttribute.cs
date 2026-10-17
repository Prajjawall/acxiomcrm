using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxPhoneAttribute : ValidationAttribute
{
    public AxPhoneAttribute()
    {
        ErrorMessage = "Enter a valid phone number.";
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

            return PhoneRules.IsValid(s);
        }

        return false;
    }
}
