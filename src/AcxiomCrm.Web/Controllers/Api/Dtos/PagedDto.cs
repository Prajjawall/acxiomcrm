namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record PagedDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);
