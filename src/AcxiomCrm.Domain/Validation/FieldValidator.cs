namespace AcxiomCrm.Domain.Validation;

public sealed class FieldValidator
{
    private readonly List<FieldError> _errors = [];
    private readonly HashSet<string> _failedFields = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<FieldError> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public FieldValidator Required(string field, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(field, Messages.Required(label));
        }

        return this;
    }

    public FieldValidator MaxLength(string field, string label, string? value, int max)
    {
        if (value is not null && value.Length > max)
        {
            AddError(field, Messages.MaxLength(label, max));
        }

        return this;
    }

    public FieldValidator Email(string field, string? value, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                AddError(field, Messages.InvalidEmail);
            }

            return this;
        }

        if (!EmailRules.IsValid(value))
        {
            AddError(field, Messages.InvalidEmail);
        }

        return this;
    }

    public FieldValidator Phone(string field, string? value, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                AddError(field, Messages.InvalidPhone);
            }

            return this;
        }

        if (!PhoneRules.IsValid(value))
        {
            AddError(field, Messages.InvalidPhone);
        }

        return this;
    }

    public FieldValidator Check(bool ok, string field, string message)
    {
        if (!ok)
        {
            AddError(field, message);
        }

        return this;
    }

    private void AddError(string field, string message)
    {
        if (_failedFields.Add(field))
        {
            _errors.Add(new FieldError(field, message));
        }
    }
}
