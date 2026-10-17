using System.Diagnostics.CodeAnalysis;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Leads;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Contract defined in plan")]
public interface ILeadService
{
    Task<PagedResult<LeadListItem>> SearchAsync(LeadFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<LeadDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<LeadDetail>> CreateAsync(CreateLeadCommand c, CancellationToken ct = default);
    Task<Result<LeadDetail>> UpdateAsync(int id, UpdateLeadCommand c, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<LeadDetail>> ChangeStatusAsync(int id, LeadStatus to, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<LeadDetail>> AssignAsync(int id, string ownerId, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<ConvertLeadResult>> ConvertAsync(int id, ConvertLeadCommand c, CancellationToken ct = default);
    Task<int?> FindConvertibleDuplicateCustomerIdAsync(int leadId, CancellationToken ct = default);
}
