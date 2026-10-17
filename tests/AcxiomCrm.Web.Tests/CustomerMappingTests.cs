using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomerMappingTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToCreateCommand_maps_form_properties_accurately()
    {
        var form = new CustomerFormViewModel
        {
            Name = "Acme Corp",
            CompanyName = "Acme Global",
            Email = "acme@example.com",
            Phone = "9876543210",
            Address = "123 Main St",
            City = "Mumbai",
            State = "Maharashtra",
            Status = CustomerStatus.Active,
            Notes = "Important client",
            OwnerId = "user-123"
        };

        var cmd = form.ToCreateCommand();

        cmd.Name.ShouldBe("Acme Corp");
        cmd.CompanyName.ShouldBe("Acme Global");
        cmd.Email.ShouldBe("acme@example.com");
        cmd.Phone.ShouldBe("9876543210");
        cmd.Address.ShouldBe("123 Main St");
        cmd.City.ShouldBe("Mumbai");
        cmd.State.ShouldBe("Maharashtra");
        cmd.Status.ShouldBe(CustomerStatus.Active);
        cmd.Notes.ShouldBe("Important client");
        cmd.OwnerId.ShouldBe("user-123");
    }

    [Fact]
    public void ToUpdateCommand_maps_form_properties_and_decodes_rowversion()
    {
        var rawRowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var base64RowVersion = Convert.ToBase64String(rawRowVersion);

        var form = new CustomerFormViewModel
        {
            Id = 42,
            Name = "Acme Corp",
            CompanyName = "Acme Global",
            Email = "acme@example.com",
            Phone = "9876543210",
            Address = "123 Main St",
            City = "Mumbai",
            State = "Maharashtra",
            Status = CustomerStatus.Inactive,
            Notes = "Important client",
            OwnerId = "user-123",
            RowVersion = base64RowVersion
        };

        var cmd = form.ToUpdateCommand();

        cmd.Name.ShouldBe("Acme Corp");
        cmd.CompanyName.ShouldBe("Acme Global");
        cmd.Email.ShouldBe("acme@example.com");
        cmd.Phone.ShouldBe("9876543210");
        cmd.Address.ShouldBe("123 Main St");
        cmd.City.ShouldBe("Mumbai");
        cmd.State.ShouldBe("Maharashtra");
        cmd.Status.ShouldBe(CustomerStatus.Inactive);
        cmd.Notes.ShouldBe("Important client");
        cmd.OwnerId.ShouldBe("user-123");
        cmd.RowVersion.ShouldBe(rawRowVersion);
    }

    [Fact]
    public void FromDetail_maps_to_form_view_model_with_owner_options_and_base64_rowversion()
    {
        var rawRowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
        var detail = new CustomerDetail(
            Id: 10,
            Code: "CUST-000010",
            Name: "Beta Corp",
            CompanyName: "Beta Global",
            Email: "beta@example.com",
            Phone: "9123456780",
            Address: "456 Market St",
            City: "Pune",
            State: "Maharashtra",
            Status: CustomerStatus.Active,
            OwnerId: "user-456",
            OwnerName: "Jane Doe",
            Notes: "Existing client",
            CreatedDate: FixedDate,
            ModifiedDate: null,
            RowVersion: rawRowVersion
        );

        var owners = new List<UserOption>
        {
            new("user-456", "Jane Doe", "SalesExecutive"),
            new("user-789", "John Smith", "SalesManager")
        };

        var form = CustomerMapping.FromDetail(detail, owners);

        form.Id.ShouldBe(10);
        form.Name.ShouldBe("Beta Corp");
        form.CompanyName.ShouldBe("Beta Global");
        form.Email.ShouldBe("beta@example.com");
        form.Phone.ShouldBe("9123456780");
        form.Address.ShouldBe("456 Market St");
        form.City.ShouldBe("Pune");
        form.State.ShouldBe("Maharashtra");
        form.Status.ShouldBe(CustomerStatus.Active);
        form.OwnerId.ShouldBe("user-456");
        form.Notes.ShouldBe("Existing client");
        form.RowVersion.ShouldBe(Convert.ToBase64String(rawRowVersion));
        form.OwnerOptions.ShouldBe(owners);
    }

    [Fact]
    public void ApplyErrors_maps_field_errors_to_proper_model_state_keys()
    {
        var modelState = new ModelStateDictionary();
        var errors = new List<FieldError>
        {
            new("Email", "A customer with this email already exists."),
            new("Phone", "Enter a valid phone number."),
            new("Name", "Customer name is required."),
            new("", "Concurrent update detected. Please reload."),
            new("UnknownField", "General error")
        };

        modelState.ApplyErrors(errors);

        modelState.IsValid.ShouldBeFalse();
        modelState.ErrorCount.ShouldBe(5);

        modelState[nameof(CustomerFormViewModel.Email)]!.Errors[0].ErrorMessage
            .ShouldBe("A customer with this email already exists.");
        modelState[nameof(CustomerFormViewModel.Phone)]!.Errors[0].ErrorMessage
            .ShouldBe("Enter a valid phone number.");
        modelState[nameof(CustomerFormViewModel.Name)]!.Errors[0].ErrorMessage
            .ShouldBe("Customer name is required.");
        modelState[""]!.Errors[0].ErrorMessage
            .ShouldBe("Concurrent update detected. Please reload.");
        modelState[""]!.Errors[1].ErrorMessage
            .ShouldBe("General error");
    }
}
