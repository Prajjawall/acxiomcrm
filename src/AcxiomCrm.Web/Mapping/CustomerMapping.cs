using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class CustomerMapping
{
    public static CreateCustomerCommand ToCreateCommand(this CustomerFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CreateCustomerCommand(
            model.Name,
            model.CompanyName,
            model.Email,
            model.Phone,
            model.Address,
            model.City,
            model.State,
            model.Status,
            model.OwnerId,
            model.Notes);
    }

    public static UpdateCustomerCommand ToUpdateCommand(this CustomerFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        return new UpdateCustomerCommand(
            model.Name,
            model.CompanyName,
            model.Email,
            model.Phone,
            model.Address,
            model.City,
            model.State,
            model.Status,
            model.OwnerId,
            model.Notes,
            rowVersion);
    }

    public static CustomerFormViewModel FromDetail(CustomerDetail detail, IReadOnlyList<UserOption> ownerOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new CustomerFormViewModel
        {
            Id = detail.Id,
            Name = detail.Name,
            CompanyName = detail.CompanyName,
            Email = detail.Email,
            Phone = detail.Phone,
            Address = detail.Address,
            City = detail.City,
            State = detail.State,
            Status = detail.Status,
            OwnerId = detail.OwnerId,
            Notes = detail.Notes,
            RowVersion = Convert.ToBase64String(detail.RowVersion),
            OwnerOptions = ownerOptions
        };
    }

    public static void ApplyErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        if (errors is null)
        {
            return;
        }

        foreach (var error in errors)
        {
            var key = error.Field switch
            {
                "Name" => nameof(CustomerFormViewModel.Name),
                "CompanyName" => nameof(CustomerFormViewModel.CompanyName),
                "Email" => nameof(CustomerFormViewModel.Email),
                "Phone" => nameof(CustomerFormViewModel.Phone),
                "Address" => nameof(CustomerFormViewModel.Address),
                "City" => nameof(CustomerFormViewModel.City),
                "State" => nameof(CustomerFormViewModel.State),
                "Notes" => nameof(CustomerFormViewModel.Notes),
                "OwnerId" => nameof(CustomerFormViewModel.OwnerId),
                "Status" => nameof(CustomerFormViewModel.Status),
                "RowVersion" => nameof(CustomerFormViewModel.RowVersion),
                _ => string.Empty
            };

            modelState.AddModelError(key, error.Message);
        }
    }
}
