using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxRequiredAttribute : RequiredAttribute
{
    public AxRequiredAttribute(string label)
    {
        Label = label;
        ErrorMessage = $"{label} is required.";
        AllowEmptyStrings = false;
    }

    public string Label { get; }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string s && string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        return base.IsValid(value);
    }
}
