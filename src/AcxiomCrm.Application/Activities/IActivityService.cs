using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Activities;

public interface IActivityService
{
    Task<PagedResult<ActivityListItem>> SearchAsync(ActivityFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<ActivityDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<ActivityDetail>> CreateAsync(CreateActivityCommand c, CancellationToken ct = default);
    Task<Result<ActivityDetail>> UpdateAsync(int id, UpdateActivityCommand c, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
