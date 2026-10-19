using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.FollowUps;

public interface IFollowUpService
{
    Task<PagedResult<FollowUpListItem>> SearchAsync(FollowUpFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CreateAsync(CreateFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> UpdateAsync(int id, UpdateFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CompleteAsync(int id, CompleteFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> RescheduleAsync(int id, DateTime newDateUtc, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> MarkMissedAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CancelAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<NotificationCounts> GetNotificationCountsAsync(CancellationToken ct = default);
}
