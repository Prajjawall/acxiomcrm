namespace AcxiomCrm.Application.Common;

public sealed record PageQuery(int Page = 1, int PageSize = 25, string? Sort = null, bool Desc = false)
{
    public int Page { get; init; } = Math.Max(1, Page);
    public int PageSize { get; init; } = Math.Clamp(PageSize, 1, 100);
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;

    public PageQuery Normalised() => new(Math.Max(1, Page), Math.Clamp(PageSize, 1, 100), Sort, Desc);
}
