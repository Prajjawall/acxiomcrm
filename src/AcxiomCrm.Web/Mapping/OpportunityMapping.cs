using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class OpportunityMapping
{
    public static CreateOpportunityCommand ToCreateCommand(this OpportunityFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CreateOpportunityCommand(
            model.Name,
            model.CustomerId,
            model.Amount,
            model.Probability,
            model.ExpectedCloseDate ?? DateOnly.MinValue,
            model.Stage,
            model.Source,
            string.IsNullOrWhiteSpace(model.OwnerId) ? null : model.OwnerId,
            string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes);
    }

    public static UpdateOpportunityCommand ToUpdateCommand(this OpportunityFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        return new UpdateOpportunityCommand(
            model.Name,
            model.CustomerId,
            model.Amount,
            model.Probability,
            model.ExpectedCloseDate ?? DateOnly.MinValue,
            model.Source,
            string.IsNullOrWhiteSpace(model.OwnerId) ? null : model.OwnerId,
            string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes,
            rowVersion);
    }

    public static OpportunityFormViewModel FromDetail(OpportunityDetail detail, IReadOnlyList<UserOption> ownerOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new OpportunityFormViewModel
        {
            Id = detail.Id,
            Name = detail.Name,
            CustomerId = detail.CustomerId,
            CustomerName = detail.CustomerName,
            Amount = detail.Amount,
            Probability = detail.Probability,
            ExpectedCloseDate = detail.ExpectedCloseDate,
            Stage = detail.Stage,
            Source = detail.Source,
            OwnerId = detail.OwnerId,
            Notes = detail.Notes,
            RowVersion = Convert.ToBase64String(detail.RowVersion),
            OwnerOptions = ownerOptions
        };
    }

    public static void ApplyOpportunityErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
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
                "Name" => nameof(OpportunityFormViewModel.Name),
                "CustomerId" => nameof(OpportunityFormViewModel.CustomerId),
                "Amount" => nameof(OpportunityFormViewModel.Amount),
                "Probability" => nameof(OpportunityFormViewModel.Probability),
                "ExpectedCloseDate" => nameof(OpportunityFormViewModel.ExpectedCloseDate),
                "Stage" => nameof(OpportunityFormViewModel.Stage),
                "Source" => nameof(OpportunityFormViewModel.Source),
                "OwnerId" => nameof(OpportunityFormViewModel.OwnerId),
                "Notes" => nameof(OpportunityFormViewModel.Notes),
                "CloseNote" => "CloseNote",
                _ => string.Empty
            };

            modelState.AddModelError(key, error.Message);
        }
    }
}
