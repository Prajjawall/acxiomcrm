using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Customers;

public class CustomerService : ICustomerService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOwnerScope _ownerScope;
    private readonly IUserDirectory _userDirectory;
    private readonly IClock _clock;

    public CustomerService(
        CrmDbContext db,
        ICurrentUser currentUser,
        IOwnerScope ownerScope,
        IUserDirectory userDirectory,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _ownerScope = ownerScope;
        _userDirectory = userDirectory;
        _clock = clock;
    }

    public async Task<PagedResult<CustomerListItem>> SearchAsync(CustomerFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.CustomersView))
        {
            return new PagedResult<CustomerListItem>([], p.Page, p.PageSize, 0);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Customers.AsNoTracking().InScope(scope);

        if (f.Status.HasValue)
        {
            query = query.Where(c => c.Status == f.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.OwnerId))
        {
            query = query.Where(c => c.OwnerId == f.OwnerId);
        }

        var term = SearchHelper.Normalise(f.Search);
        if (term is not null)
        {
            var pattern = $"%{SearchHelper.EscapeLike(term)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Name, pattern, @"\") ||
                EF.Functions.Like(c.CompanyName ?? "", pattern, @"\") ||
                EF.Functions.Like(c.Email, pattern, @"\") ||
                EF.Functions.Like(c.Phone, pattern, @"\"));
        }

        var total = await query.CountAsync(ct);

        var sort = p.Sort?.ToLowerInvariant();
        query = sort switch
        {
            "name" => p.Desc
                ? query.OrderByDescending(c => c.Name).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.Name).ThenBy(c => c.Id),
            "code" => p.Desc
                ? query.OrderByDescending(c => c.Code).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.Code).ThenBy(c => c.Id),
            "status" => p.Desc
                ? query.OrderByDescending(c => c.Status).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.Status).ThenBy(c => c.Id),
            "createddate" => p.Desc
                ? query.OrderByDescending(c => c.CreatedDate).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.CreatedDate).ThenBy(c => c.Id),
            _ => p.Desc
                ? query.OrderByDescending(c => c.CreatedDate).ThenByDescending(c => c.Id)
                : query.OrderByDescending(c => c.CreatedDate).ThenByDescending(c => c.Id)
        };

        var items = await (
            from c in query.Skip(p.Skip).Take(p.Take)
            join u in _db.Users on c.OwnerId equals u.Id into users
            from u in users.DefaultIfEmpty()
            select new CustomerListItem(
                c.Id,
                c.Code,
                c.Name,
                c.CompanyName,
                c.Email,
                c.Phone,
                c.Status,
                c.OwnerId,
                u != null ? u.FullName : string.Empty,
                c.CreatedDate
            )
        ).ToListAsync(ct);

        return new PagedResult<CustomerListItem>(items, p.Page, p.PageSize, total);
    }

    public async Task<Result<CustomerDetail>> GetAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.CustomersView))
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);

        var item = await (
            from c in _db.Customers.AsNoTracking().InScope(scope)
            where c.Id == id
            join u in _db.Users on c.OwnerId equals u.Id into users
            from u in users.DefaultIfEmpty()
            select new CustomerDetail(
                c.Id,
                c.Code,
                c.Name,
                c.CompanyName,
                c.Email,
                c.Phone,
                c.Address,
                c.City,
                c.State,
                c.Status,
                c.OwnerId,
                u != null ? u.FullName : string.Empty,
                c.Notes,
                c.CreatedDate,
                c.ModifiedDate,
                c.RowVersion
            )
        ).FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return Result<CustomerDetail>.Fail(ErrorKind.NotFound);
        }

        return Result<CustomerDetail>.Ok(item);
    }

    public async Task<Result<CustomerDetail>> CreateAsync(CreateCustomerCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.CustomersCreate))
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Forbidden);
        }

        var validationErrors = CustomerRules.Validate(
            c.Name, c.CompanyName, c.Email, c.Phone, c.Address, c.City, c.State, c.Notes).ToList();

        if (!Enum.IsDefined(c.Status))
        {
            validationErrors.Add(new FieldError("Status", "The selected status is invalid."));
        }

        string targetOwnerId;
        if (string.Equals(_currentUser.Role, Roles.SalesExecutive, StringComparison.OrdinalIgnoreCase))
        {
            targetOwnerId = _currentUser.UserId ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(c.OwnerId) && c.OwnerId != _currentUser.UserId)
        {
            targetOwnerId = c.OwnerId;
            var assignableOwners = await _userDirectory.ListAssignableOwnersAsync(ct);
            if (!assignableOwners.Any(o => o.Id == targetOwnerId))
            {
                validationErrors.Add(new FieldError("OwnerId", "The selected owner is invalid."));
            }
        }
        else
        {
            targetOwnerId = _currentUser.UserId ?? string.Empty;
        }

        if (validationErrors.Count > 0)
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Validation, validationErrors);
        }

        var dupErrors = await CustomerCreator.CheckDuplicatesAsync(_db, c.Email, c.Phone, ct: ct);
        if (dupErrors.Count > 0)
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Conflict, dupErrors);
        }

        var customer = CustomerCreator.CreateEntity(
            c.Name,
            c.CompanyName,
            c.Email,
            c.Phone,
            c.Address,
            c.City,
            c.State,
            c.Status,
            targetOwnerId,
            c.Notes);

        _db.Customers.Add(customer);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            var sqlErrors = new List<FieldError>();
            if (sqlEx.Message.Contains("UX_Customers_EmailNormalized", StringComparison.OrdinalIgnoreCase))
            {
                sqlErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            }
            if (sqlEx.Message.Contains("UX_Customers_Phone", StringComparison.OrdinalIgnoreCase))
            {
                sqlErrors.Add(new FieldError("Phone", Messages.DuplicatePhone));
            }
            if (sqlErrors.Count == 0)
            {
                sqlErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            }
            return Result<CustomerDetail>.Fail(ErrorKind.Conflict, sqlErrors);
        }

        var ownerOption = await _userDirectory.GetOptionAsync(customer.OwnerId, ct);
        var detail = new CustomerDetail(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.CompanyName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.State,
            customer.Status,
            customer.OwnerId,
            ownerOption?.Name ?? string.Empty,
            customer.Notes,
            customer.CreatedDate,
            customer.ModifiedDate,
            customer.RowVersion);

        return Result<CustomerDetail>.Ok(detail);
    }

    public async Task<Result<CustomerDetail>> UpdateAsync(int id, UpdateCustomerCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.CustomersEdit))
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Forbidden);
        }

        var validationErrors = CustomerRules.Validate(
            c.Name, c.CompanyName, c.Email, c.Phone, c.Address, c.City, c.State, c.Notes).ToList();

        if (!Enum.IsDefined(c.Status))
        {
            validationErrors.Add(new FieldError("Status", "The selected status is invalid."));
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var customer = await _db.Customers.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (customer is null)
        {
            return Result<CustomerDetail>.Fail(ErrorKind.NotFound);
        }

        string targetOwnerId = customer.OwnerId;
        if (!string.Equals(_currentUser.Role, Roles.SalesExecutive, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(c.OwnerId) && c.OwnerId != customer.OwnerId)
            {
                if (c.OwnerId == _currentUser.UserId)
                {
                    targetOwnerId = _currentUser.UserId;
                }
                else
                {
                    var assignableOwners = await _userDirectory.ListAssignableOwnersAsync(ct);
                    if (!assignableOwners.Any(o => o.Id == c.OwnerId))
                    {
                        validationErrors.Add(new FieldError("OwnerId", "The selected owner is invalid."));
                    }
                    else
                    {
                        targetOwnerId = c.OwnerId;
                    }
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            return Result<CustomerDetail>.Fail(ErrorKind.Validation, validationErrors);
        }

        var emailNormalized = EmailRules.Normalize(c.Email);
        var hasNormalizedPhone = PhoneRules.TryNormalize(c.Phone, out var normalizedPhone);

        var emailExists = await _db.Customers.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Id != id && x.EmailNormalized == emailNormalized, ct);
        var phoneExists = hasNormalizedPhone && await _db.Customers.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Id != id && x.Phone == normalizedPhone, ct);

        if (emailExists || phoneExists)
        {
            var dupErrors = new List<FieldError>();
            if (emailExists) dupErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            if (phoneExists) dupErrors.Add(new FieldError("Phone", Messages.DuplicatePhone));
            return Result<CustomerDetail>.Fail(ErrorKind.Conflict, dupErrors);
        }

        if (c.RowVersion is not null && c.RowVersion.Length > 0)
        {
            _db.Entry(customer).Property(x => x.RowVersion).OriginalValue = c.RowVersion;
        }

        customer.Update(
            c.Name,
            c.CompanyName,
            c.Email,
            c.Phone,
            c.Address,
            c.City,
            c.State,
            c.Status,
            targetOwnerId,
            c.Notes);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<CustomerDetail>("Customer");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            var dupErrors = new List<FieldError>();
            if (sqlEx.Message.Contains("UX_Customers_EmailNormalized", StringComparison.OrdinalIgnoreCase))
            {
                dupErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            }
            if (sqlEx.Message.Contains("UX_Customers_Phone", StringComparison.OrdinalIgnoreCase))
            {
                dupErrors.Add(new FieldError("Phone", Messages.DuplicatePhone));
            }
            if (dupErrors.Count == 0)
            {
                dupErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            }
            return Result<CustomerDetail>.Fail(ErrorKind.Conflict, dupErrors);
        }

        var ownerOption = await _userDirectory.GetOptionAsync(customer.OwnerId, ct);
        var detail = new CustomerDetail(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.CompanyName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.State,
            customer.Status,
            customer.OwnerId,
            ownerOption?.Name ?? string.Empty,
            customer.Notes,
            customer.CreatedDate,
            customer.ModifiedDate,
            customer.RowVersion);

        return Result<CustomerDetail>.Ok(detail);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var customer = await _db.Customers.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (customer is null)
        {
            return Result.Fail(ErrorKind.NotFound);
        }

        if (!_currentUser.Can(Permissions.CustomersDelete))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var hasOpenOpportunities = await _db.Opportunities
            .AnyAsync(o => o.CustomerId == id && o.Status == OpportunityStatus.Open, ct);
        if (hasOpenOpportunities)
        {
            return Result.Fail(ErrorKind.Conflict, [new FieldError(string.Empty, "This customer has open opportunities. Close or reassign them first.")]);
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<CustomerLookupItem>> LookupAsync(string? term, int take = 20, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.CustomersView))
        {
            return [];
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var limit = Math.Clamp(take, 1, 100);
        var query = _db.Customers.AsNoTracking().InScope(scope).Where(c => c.Status == CustomerStatus.Active);

        var normTerm = SearchHelper.Normalise(term);
        if (normTerm is not null)
        {
            var pattern = $"%{SearchHelper.EscapeLike(normTerm)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Name, pattern, @"\") ||
                EF.Functions.Like(c.CompanyName ?? "", pattern, @"\") ||
                EF.Functions.Like(c.Code, pattern, @"\"));
        }

        return await query
            .OrderBy(c => c.Name)
            .Take(limit)
            .Select(c => new CustomerLookupItem(c.Id, c.Code, c.Name, c.CompanyName))
            .ToListAsync(ct);
    }

    public async Task<CustomerDuplicate?> FindDuplicateAsync(string email, string phone, int? excludeId = null, CancellationToken ct = default)
    {
        var dupErrors = await CustomerCreator.CheckDuplicatesAsync(_db, email, phone, excludeId, ct);
        if (dupErrors.Any(e => e.Field == "Email"))
        {
            return new CustomerDuplicate("Email");
        }
        if (dupErrors.Any(e => e.Field == "Phone"))
        {
            return new CustomerDuplicate("Phone");
        }
        return null;
    }
}
