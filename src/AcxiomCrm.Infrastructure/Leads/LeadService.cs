using System.Globalization;
using System.Text.Json;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Customers;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Leads;

public class LeadService : ILeadService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOwnerScope _ownerScope;
    private readonly IAuditActionHint _actionHint;
    private readonly IClock _clock;
    private readonly IOpportunityFactory _opportunityFactory;

    public LeadService(
        CrmDbContext db,
        ICurrentUser currentUser,
        IOwnerScope ownerScope,
        IAuditActionHint actionHint,
        IClock clock,
        IOpportunityFactory opportunityFactory)
    {
        _db = db;
        _currentUser = currentUser;
        _ownerScope = ownerScope;
        _actionHint = actionHint;
        _clock = clock;
        _opportunityFactory = opportunityFactory;
    }

    public async Task<PagedResult<LeadListItem>> SearchAsync(LeadFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsView))
        {
            return new PagedResult<LeadListItem>([], p.Page, p.PageSize, 0);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Leads.AsNoTracking().InScope(scope);

        if (f.Status.HasValue)
        {
            query = query.Where(l => l.Status == f.Status.Value);
        }

        if (f.Priority.HasValue)
        {
            query = query.Where(l => l.Priority == f.Priority.Value);
        }

        if (f.Source.HasValue)
        {
            query = query.Where(l => l.Source == f.Source.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.OwnerId))
        {
            query = query.Where(l => l.OwnerId == f.OwnerId);
        }

        var term = SearchHelper.Normalise(f.Search);
        if (term is not null)
        {
            var pattern = $"%{SearchHelper.EscapeLike(term)}%";
            query = query.Where(l =>
                EF.Functions.Like(l.Name, pattern, @"\") ||
                EF.Functions.Like(l.CompanyName ?? "", pattern, @"\") ||
                EF.Functions.Like(l.Email ?? "", pattern, @"\") ||
                EF.Functions.Like(l.Phone ?? "", pattern, @"\"));
        }

        var total = await query.CountAsync(ct);

        var sort = p.Sort?.ToLowerInvariant();
        query = sort switch
        {
            "name" => p.Desc
                ? query.OrderByDescending(l => l.Name).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.Name).ThenBy(l => l.Id),
            "code" => p.Desc
                ? query.OrderByDescending(l => l.Code).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.Code).ThenBy(l => l.Id),
            "status" => p.Desc
                ? query.OrderByDescending(l => l.Status).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.Status).ThenBy(l => l.Id),
            "priority" => p.Desc
                ? query.OrderByDescending(l => l.Priority).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.Priority).ThenBy(l => l.Id),
            "expectedvalue" => p.Desc
                ? query.OrderByDescending(l => l.ExpectedValue).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.ExpectedValue).ThenBy(l => l.Id),
            "createddate" => p.Desc
                ? query.OrderByDescending(l => l.CreatedDate).ThenByDescending(l => l.Id)
                : query.OrderBy(l => l.CreatedDate).ThenBy(l => l.Id),
            _ => p.Desc
                ? query.OrderByDescending(l => l.CreatedDate).ThenByDescending(l => l.Id)
                : query.OrderByDescending(l => l.CreatedDate).ThenByDescending(l => l.Id)
        };

        var items = await (
            from l in query.Skip(p.Skip).Take(p.Take)
            join u in _db.Users on l.OwnerId equals u.Id into users
            from u in users.DefaultIfEmpty()
            select new LeadListItem(
                l.Id,
                l.Code,
                l.Name,
                l.CompanyName,
                l.Email,
                l.Phone,
                l.Source,
                l.Status,
                l.Priority,
                l.ExpectedValue,
                l.OwnerId,
                u != null ? (u.FullName ?? u.UserName ?? l.OwnerId) : l.OwnerId,
                l.CreatedDate)
        ).ToListAsync(ct);

        return new PagedResult<LeadListItem>(items, p.Page, p.PageSize, total);
    }

    public async Task<Result<LeadDetail>> GetAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsView))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("Id", "You do not have permission to view leads."));
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var item = await (
            from l in _db.Leads.AsNoTracking().InScope(scope)
            where l.Id == id
            join u in _db.Users on l.OwnerId equals u.Id into users
            from u in users.DefaultIfEmpty()
            join c in _db.Customers on l.ConvertedCustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            select new
            {
                Lead = l,
                OwnerName = u != null ? (u.FullName ?? u.UserName ?? l.OwnerId) : l.OwnerId,
                CustomerName = c != null ? c.Name : null
            }
        ).FirstOrDefaultAsync(ct);

        if (item == null)
        {
            return Result<LeadDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        var allowedTransitions = LeadWorkflow.AllowedFrom(item.Lead.Status, _currentUser.Role);

        return Result<LeadDetail>.Ok(new LeadDetail(
            item.Lead.Id,
            item.Lead.Code,
            item.Lead.Name,
            item.Lead.CompanyName,
            item.Lead.Email,
            item.Lead.Phone,
            item.Lead.Source,
            item.Lead.Status,
            item.Lead.Priority,
            item.Lead.ExpectedValue,
            item.Lead.OwnerId,
            item.OwnerName,
            item.Lead.Notes,
            item.Lead.ConvertedCustomerId,
            item.CustomerName,
            item.Lead.ConvertedOpportunityId,
            item.Lead.ConvertedDate,
            item.Lead.CreatedDate,
            item.Lead.ModifiedDate,
            item.Lead.RowVersion,
            allowedTransitions));
    }

    public async Task<Result<LeadDetail>> CreateAsync(CreateLeadCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsCreate))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("Name", "You do not have permission to create leads."));
        }

        var errors = LeadRules.Validate(c.Name, c.CompanyName, c.Email, c.Phone, c.ExpectedValue, c.Notes);
        if (errors.Count > 0)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Validation, errors);
        }

        string ownerId;
        if (string.Equals(_currentUser.Role, Roles.SalesExecutive, StringComparison.OrdinalIgnoreCase))
        {
            ownerId = _currentUser.UserId ?? string.Empty;
        }
        else
        {
            ownerId = string.IsNullOrWhiteSpace(c.OwnerId) ? (_currentUser.UserId ?? string.Empty) : c.OwnerId;

            var targetUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId, ct);
            if (targetUser == null || !targetUser.IsActive)
            {
                return Result<LeadDetail>.Fail(ErrorKind.Validation, new FieldError("OwnerId", "Cannot assign to an inactive user."));
            }

            var scope = await _ownerScope.ResolveAsync(ct);
            if (!scope.Contains(ownerId))
            {
                return Result<LeadDetail>.Fail(ErrorKind.Validation, new FieldError("OwnerId", "Target owner is outside your scope."));
            }
        }

        var lead = Lead.Create(
            c.Name,
            c.CompanyName,
            c.Email,
            c.Phone,
            c.Source,
            c.Priority,
            c.ExpectedValue,
            ownerId,
            c.Notes);

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(lead.Id, ct);
    }

    public async Task<Result<LeadDetail>> UpdateAsync(int id, UpdateLeadCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsEdit))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("Id", "You do not have permission to edit leads."));
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.InScope(scope).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null)
        {
            return Result<LeadDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        if (lead.Status is LeadStatus.Converted or LeadStatus.Lost)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("Status", $"This lead is {lead.Status} and cannot be edited."));
        }

        if (c.RowVersion is not null && !lead.RowVersion.SequenceEqual(c.RowVersion))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        var errors = LeadRules.Validate(c.Name, c.CompanyName, c.Email, c.Phone, c.ExpectedValue, c.Notes);
        if (errors.Count > 0)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Validation, errors);
        }

        lead.UpdateDetails(c.Name, c.CompanyName, c.Email, c.Phone, c.Source, c.Priority, c.ExpectedValue, c.Notes);

        if (c.RowVersion is not null)
        {
            _db.Entry(lead).OriginalValues["RowVersion"] = c.RowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        return await GetAsync(lead.Id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.InScope(scope).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null)
        {
            return Result.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        if (!_currentUser.Can(Permissions.LeadsDelete))
        {
            return Result.Fail(ErrorKind.Forbidden, new FieldError("Id", "You do not have permission to delete leads."));
        }

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync(ct);

        return Result.Ok();
    }

    public async Task<Result<LeadDetail>> ChangeStatusAsync(int id, LeadStatus to, byte[] rowVersion, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsEdit))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("Id", "You do not have permission to edit leads."));
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.InScope(scope).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null)
        {
            return Result<LeadDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        if (rowVersion is not null && !lead.RowVersion.SequenceEqual(rowVersion))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        if (lead.Status == LeadStatus.Lost && to == LeadStatus.New)
        {
            if (_currentUser.Role is not Roles.Admin and not Roles.Manager)
            {
                return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("Status", "Only managers and administrators can reopen a lost lead."));
            }
        }

        if (to == LeadStatus.Converted)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("Status", $"A lead cannot move from {lead.Status} to Converted."));
        }

        if (!LeadWorkflow.CanTransition(lead.Status, to))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("Status", $"A lead cannot move from {lead.Status} to {to}."));
        }

        lead.MoveTo(to);
        _actionHint.Set(AuditActions.StatusChanged);

        if (rowVersion is not null)
        {
            _db.Entry(lead).OriginalValues["RowVersion"] = rowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        return await GetAsync(lead.Id, ct);
    }

    public async Task<Result<LeadDetail>> AssignAsync(int id, string ownerId, byte[] rowVersion, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.InScope(scope).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null)
        {
            return Result<LeadDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        if (!_currentUser.Can(Permissions.LeadsAssign))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Forbidden, new FieldError("OwnerId", "You do not have permission to assign leads."));
        }

        if (rowVersion is not null && !lead.RowVersion.SequenceEqual(rowVersion))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        var targetUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId, ct);
        if (targetUser == null || !targetUser.IsActive)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Validation, new FieldError("OwnerId", "Cannot assign to an inactive user."));
        }

        if (!scope.Contains(ownerId))
        {
            return Result<LeadDetail>.Fail(ErrorKind.Validation, new FieldError("OwnerId", "Target owner is outside your scope."));
        }

        lead.AssignTo(ownerId);
        _actionHint.Set(AuditActions.Assigned);

        if (rowVersion is not null)
        {
            _db.Entry(lead).OriginalValues["RowVersion"] = rowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        return await GetAsync(lead.Id, ct);
    }

    public async Task<Result<ConvertLeadResult>> ConvertAsync(int id, ConvertLeadCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.LeadsConvert))
        {
            return Result<ConvertLeadResult>.Fail(ErrorKind.Forbidden, new FieldError("Id", "You do not have permission to convert leads."));
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.InScope(scope).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null)
        {
            return Result<ConvertLeadResult>.Fail(ErrorKind.NotFound, new FieldError("Id", "Lead not found."));
        }

        if (c.RowVersion is not null && !lead.RowVersion.SequenceEqual(c.RowVersion))
        {
            return Result<ConvertLeadResult>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }

        if (lead.Status != LeadStatus.Qualified)
        {
            return Result<ConvertLeadResult>.Fail(ErrorKind.Conflict, new FieldError("Status", "Only qualified leads can be converted."));
        }

        if (c.CreateOpportunity && c.Opportunity == null)
        {
            return Result<ConvertLeadResult>.Fail(ErrorKind.Validation, new FieldError("Opportunity", "Opportunity details are required when creating an opportunity."));
        }

        Customer? existingCustomer = null;
        if (c.ExistingCustomerId.HasValue)
        {
            existingCustomer = await _db.Customers.InScope(scope).FirstOrDefaultAsync(x => x.Id == c.ExistingCustomerId.Value, ct);
            if (existingCustomer == null)
            {
                return Result<ConvertLeadResult>.Fail(ErrorKind.NotFound, new FieldError("ExistingCustomerId", "Existing customer not found or outside your scope."));
            }
            if (existingCustomer.Status != CustomerStatus.Active)
            {
                return Result<ConvertLeadResult>.Fail(ErrorKind.Validation, new FieldError("ExistingCustomerId", "Existing customer must be active."));
            }
        }
        else
        {
            var contactErrors = new List<FieldError>();
            if (string.IsNullOrWhiteSpace(lead.Email))
            {
                contactErrors.Add(new FieldError("Email", "Add an email address and phone number to the lead before converting."));
            }
            if (string.IsNullOrWhiteSpace(lead.Phone))
            {
                contactErrors.Add(new FieldError("Phone", "Add an email address and phone number to the lead before converting."));
            }
            if (contactErrors.Count > 0)
            {
                return Result<ConvertLeadResult>.Fail(ErrorKind.Validation, contactErrors);
            }

            var dupErrors = await CustomerCreator.CheckDuplicatesAsync(_db, lead.Email!, lead.Phone!, ct: ct);
            if (dupErrors.Count > 0)
            {
                return Result<ConvertLeadResult>.Fail(ErrorKind.Conflict, new FieldError("Email", "A customer with this email already exists. Link to the existing customer instead."));
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            int customerId;
            if (existingCustomer != null)
            {
                customerId = existingCustomer.Id;
            }
            else
            {
                var newCustomer = CustomerCreator.CreateEntity(
                    lead.Name,
                    lead.CompanyName,
                    lead.Email!,
                    lead.Phone!,
                    null,
                    null,
                    null,
                    CustomerStatus.Active,
                    lead.OwnerId,
                    null);

                _db.Customers.Add(newCustomer);
                await _db.SaveChangesAsync(ct);
                customerId = newCustomer.Id;
            }

            int? opportunityId = null;
            if (c.CreateOpportunity)
            {
                var oppResult = await _opportunityFactory.CreateForConversionAsync(
                    customerId, lead.Id, lead.OwnerId, c.Opportunity!, ct);
                if (!oppResult.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return Result<ConvertLeadResult>.Fail(oppResult.Kind, oppResult.Errors);
                }
                opportunityId = oppResult.Value;
            }

            lead.MarkConverted(customerId, opportunityId, _clock.UtcNow);

            if (c.RowVersion is not null)
            {
                _db.Entry(lead).OriginalValues["RowVersion"] = c.RowVersion;
            }

            var auditLog = new AuditLog
            {
                CreatedDate = _clock.UtcNow,
                UserId = string.IsNullOrWhiteSpace(_currentUser?.UserId) ? "system" : _currentUser.UserId,
                UserName = _currentUser?.UserName,
                Module = AuditModules.Leads,
                Action = AuditActions.Converted,
                EntityName = nameof(Lead),
                RecordId = lead.Id.ToString(CultureInfo.InvariantCulture),
                Result = AuditResult.Success,
                NewValue = JsonSerializer.Serialize(new { customerId, opportunityId }),
                IpAddress = _currentUser?.IpAddress,
                CorrelationId = _currentUser?.CorrelationId
            };
            _db.AuditLogs.Add(auditLog);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<ConvertLeadResult>.Ok(new ConvertLeadResult(lead.Id, customerId, opportunityId));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return Result<ConvertLeadResult>.Fail(ErrorKind.Conflict, new FieldError("RowVersion", "This lead was modified by another user. Refresh and try again."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            await transaction.RollbackAsync(ct);
            return Result<ConvertLeadResult>.Fail(ErrorKind.Conflict, new FieldError("Email", "A customer with this email already exists. Link to the existing customer instead."));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int?> FindConvertibleDuplicateCustomerIdAsync(int leadId, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var lead = await _db.Leads.AsNoTracking().InScope(scope).FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(lead.Email) && string.IsNullOrWhiteSpace(lead.Phone))
        {
            return null;
        }

        var emailNormalized = !string.IsNullOrWhiteSpace(lead.Email) ? EmailRules.Normalize(lead.Email) : null;
        string? normPhone = null;
        var hasPhone = !string.IsNullOrWhiteSpace(lead.Phone) && PhoneRules.TryNormalize(lead.Phone, out normPhone);

        var customer = await _db.Customers.AsNoTracking()
            .InScope(scope)
            .Where(c => !c.IsDeleted &&
                ((emailNormalized != null && c.EmailNormalized == emailNormalized) ||
                 (hasPhone && c.Phone == normPhone)))
            .FirstOrDefaultAsync(ct);

        return customer?.Id;
    }
}
