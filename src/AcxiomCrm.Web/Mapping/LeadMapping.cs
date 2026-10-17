using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class LeadMapping
{
    public static CreateLeadCommand ToCreateCommand(this LeadFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.ToCreateCommand();
    }

    public static UpdateLeadCommand ToUpdateCommand(this LeadFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);
        return model.ToUpdateCommand(rowVersion);
    }

    public static LeadFormViewModel FromDetail(LeadDetail detail, IReadOnlyList<UserOption> ownerOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new LeadFormViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            Name = detail.Name,
            CompanyName = detail.CompanyName,
            Email = detail.Email,
            Phone = detail.Phone,
            Source = detail.Source,
            Priority = detail.Priority,
            ExpectedValue = detail.ExpectedValue,
            OwnerId = detail.OwnerId,
            Notes = detail.Notes,
            RowVersion = Convert.ToBase64String(detail.RowVersion),
            OwnerOptions = ownerOptions
        };
    }

    public static void ApplyLeadErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
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
                "Name" => nameof(LeadFormViewModel.Name),
                "CompanyName" => nameof(LeadFormViewModel.CompanyName),
                "Email" => nameof(LeadFormViewModel.Email),
                "Phone" => nameof(LeadFormViewModel.Phone),
                "Source" => nameof(LeadFormViewModel.Source),
                "Priority" => nameof(LeadFormViewModel.Priority),
                "ExpectedValue" => nameof(LeadFormViewModel.ExpectedValue),
                "Notes" => nameof(LeadFormViewModel.Notes),
                "OwnerId" => nameof(LeadFormViewModel.OwnerId),
                "Status" => "Status",
                "Opportunity" => "Opportunity",
                "RowVersion" => nameof(LeadFormViewModel.RowVersion),
                _ => string.Empty
            };

            modelState.AddModelError(key, error.Message);
        }
    }
}
