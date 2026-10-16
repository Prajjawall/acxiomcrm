using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Application.Common;

public class Result
{
    public bool Succeeded { get; }
    public ErrorKind Kind { get; }
    public IReadOnlyList<FieldError> Errors { get; }

    protected Result(bool succeeded, ErrorKind kind, IReadOnlyList<FieldError> errors)
    {
        Succeeded = succeeded;
        Kind = kind;
        Errors = errors;
    }

    public static Result Ok() => new(true, ErrorKind.None, []);

    public static Result Fail(ErrorKind kind, params FieldError[] errors)
    {
        if (kind == ErrorKind.None)
        {
            throw new ArgumentException("Cannot fail with ErrorKind.None.", nameof(kind));
        }

        return new(false, kind, errors ?? []);
    }

    public static Result Fail(ErrorKind kind, IEnumerable<FieldError> errors)
    {
        if (kind == ErrorKind.None)
        {
            throw new ArgumentException("Cannot fail with ErrorKind.None.", nameof(kind));
        }

        return new(false, kind, (errors ?? []).ToArray());
    }
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, ErrorKind.None, [])
    {
        Value = value;
    }

    private Result(ErrorKind kind, IReadOnlyList<FieldError> errors) : base(false, kind, errors)
    {
        Value = default;
    }

    public static Result<T> Ok(T value) => new(value);

    public static new Result<T> Fail(ErrorKind kind, params FieldError[] errors)
    {
        if (kind == ErrorKind.None)
        {
            throw new ArgumentException("Cannot fail with ErrorKind.None.", nameof(kind));
        }

        return new(kind, errors ?? []);
    }

    public static new Result<T> Fail(ErrorKind kind, IEnumerable<FieldError> errors)
    {
        if (kind == ErrorKind.None)
        {
            throw new ArgumentException("Cannot fail with ErrorKind.None.", nameof(kind));
        }

        return new(kind, (errors ?? []).ToArray());
    }
}
