using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxRangeAttribute : RangeAttribute
{
    public AxRangeAttribute(int minimum, int maximum, string label)
        : base(minimum, maximum)
    {
        Label = label;
        ErrorMessage = $"{label} must be between {minimum} and {maximum}.";
    }

    public AxRangeAttribute(double minimum, double maximum, string label)
        : base(minimum, maximum)
    {
        Label = label;
        ErrorMessage = $"{label} must be between {minimum} and {maximum}.";
    }

    public AxRangeAttribute(Type type, string minimum, string maximum, string label)
        : base(type, minimum, maximum)
    {
        Label = label;
        ErrorMessage = $"{label} must be between {minimum} and {maximum}.";
    }

    public string Label { get; }
}
