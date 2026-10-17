using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxMaxLengthAttribute : MaxLengthAttribute
{
    public AxMaxLengthAttribute(int length, string label) : base(length)
    {
        Label = label;
        ErrorMessage = $"{label} must not exceed {length} characters.";
    }

    public string Label { get; }
}
