using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Entities;

public sealed class Customer : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? CompanyName { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;
    public string OwnerId { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // IHasTimestamps
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime? ModifiedDate { get; private set; }
    public string? ModifiedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedDate { get; private set; }
    public string? DeletedBy { get; private set; }

    private Customer()
    {
    }

    public static Customer Create(
        string name,
        string? company,
        string email,
        string phone,
        string? address,
        string? city,
        string? state,
        CustomerStatus status,
        string ownerId,
        string? notes)
    {
        var customer = new Customer();
        customer.Update(name, company, email, phone, address, city, state, status, ownerId, notes);
        return customer;
    }

    public void Update(
        string name,
        string? company,
        string email,
        string phone,
        string? address,
        string? city,
        string? state,
        CustomerStatus status,
        string ownerId,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email) || !EmailRules.IsValid(email))
        {
            throw new ArgumentException("Enter a valid email address.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(phone) || !PhoneRules.TryNormalize(phone, out var normalizedPhone))
        {
            throw new ArgumentException("Enter a valid phone number.", nameof(phone));
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        }

        Name = name.Trim();
        CompanyName = string.IsNullOrWhiteSpace(company) ? null : company.Trim();
        Email = email.Trim();
        EmailNormalized = EmailRules.Normalize(email);
        Phone = normalizedPhone;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        Status = status;
        OwnerId = ownerId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
