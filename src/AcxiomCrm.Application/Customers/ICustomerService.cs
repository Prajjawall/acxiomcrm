using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItem>> SearchAsync(CustomerFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<CustomerDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<CustomerDetail>> CreateAsync(CreateCustomerCommand c, CancellationToken ct = default);
    Task<Result<CustomerDetail>> UpdateAsync(int id, UpdateCustomerCommand c, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerLookupItem>> LookupAsync(string? term, int take = 20, CancellationToken ct = default);
    Task<CustomerDuplicate?> FindDuplicateAsync(string email, string phone, int? excludeId = null, CancellationToken ct = default);
}
