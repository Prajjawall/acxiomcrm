using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Customers;

internal static class CustomerCreator
{
    public static Customer CreateEntity(
        string name,
        string? companyName,
        string email,
        string phone,
        string? address,
        string? city,
        string? state,
        CustomerStatus status,
        string ownerId,
        string? notes)
    {
        return Customer.Create(
            name,
            companyName,
            email,
            phone,
            address,
            city,
            state,
            status,
            ownerId,
            notes);
    }

    public static async Task<IReadOnlyList<FieldError>> CheckDuplicatesAsync(
        CrmDbContext db,
        string email,
        string phone,
        int? excludeId = null,
        CancellationToken ct = default)
    {
        var emailNormalized = EmailRules.Normalize(email);
        var hasNormalizedPhone = PhoneRules.TryNormalize(phone, out var normalizedPhone);

        var emailExists = await db.Customers.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && (excludeId == null || x.Id != excludeId.Value) && x.EmailNormalized == emailNormalized, ct);
        var phoneExists = hasNormalizedPhone && await db.Customers.IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && (excludeId == null || x.Id != excludeId.Value) && x.Phone == normalizedPhone, ct);

        if (emailExists || phoneExists)
        {
            var dupErrors = new List<FieldError>();
            if (emailExists) dupErrors.Add(new FieldError("Email", Messages.DuplicateEmail));
            if (phoneExists) dupErrors.Add(new FieldError("Phone", Messages.DuplicatePhone));
            return dupErrors;
        }

        return [];
    }
}
