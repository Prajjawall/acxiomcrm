using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Audit;

public interface IAuditQueryService
{
    Task<PagedResult<AuditListItem>> SearchAsync(AuditFilter filter, PageQuery page, CancellationToken ct = default);
    Task<Result<AuditDetail>> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListModulesAsync(CancellationToken ct = default);
    Task<PagedResult<AuditListItem>> ForRecordAsync(string entityName, string recordId, PageQuery page, CancellationToken ct = default);
}
