using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Application.Common;

public static class ConcurrencyHelper
{
    public static string ConflictMessage(string entityLabel) =>
        $"This {entityLabel.ToLowerInvariant()} was changed by someone else. Reload and try again.";

    public static Result Conflict(string entityLabel) =>
        Result.Fail(ErrorKind.Conflict, new FieldError(string.Empty, ConflictMessage(entityLabel)));

    public static Result<T> Conflict<T>(string entityLabel) =>
        Result<T>.Fail(ErrorKind.Conflict, new FieldError(string.Empty, ConflictMessage(entityLabel)));
}
