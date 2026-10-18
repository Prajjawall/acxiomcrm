using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Opportunities;

public interface IOpportunityService
{
    Task<PagedResult<OpportunityListItem>> SearchAsync(OpportunityFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> CreateAsync(CreateOpportunityCommand c, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> UpdateAsync(int id, UpdateOpportunityCommand c, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> ChangeStageAsync(int id, OpportunityStage targetStage, string? closeNote, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> ReopenAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<IReadOnlyList<BoardColumn>> GetBoardAsync(OpportunityFilter f, int maxCardsPerColumn = 50, CancellationToken ct = default);
}
