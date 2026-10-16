namespace AcxiomCrm.Web.Presentation;

public sealed record PagerModel(
    int Page,
    int PageSize,
    int Total,
    string RouteAction,
    IDictionary<string, string?>? Query = null)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;

    public int FirstItem => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Total == 0 ? 0 : Math.Min(Page * PageSize, Total);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<int> PageWindow
    {
        get
        {
            if (TotalPages <= 1)
            {
                return [];
            }

            var pages = new HashSet<int> { 1, TotalPages };
            for (var i = Math.Max(1, Page - 2); i <= Math.Min(TotalPages, Page + 2); i++)
            {
                pages.Add(i);
            }

            return pages.OrderBy(p => p).ToList();
        }
    }
}
