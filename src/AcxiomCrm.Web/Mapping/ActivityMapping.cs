using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.Activities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class ActivityMapping
{
    public static CreateActivityCommand ToCreateCommand(this ActivityFormViewModel model, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clock);

        var date = model.ActivityDate ?? clock.Today;
        var time = model.ActivityTime ?? new TimeOnly(10, 0);
        var localDateTime = date.ToDateTime(time);
        var utc = clock.ToUtcFromBusinessLocal(localDateTime);

        var related = new List<RelatedRef>();
        if (model.CustomerId.HasValue && model.CustomerId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Customer, model.CustomerId.Value));
        }
        if (model.LeadId.HasValue && model.LeadId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Lead, model.LeadId.Value));
        }
        if (model.OpportunityId.HasValue && model.OpportunityId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Opportunity, model.OpportunityId.Value));
        }

        return new CreateActivityCommand(
            model.Type,
            model.Subject,
            string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            utc,
            related,
            string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId,
            model.Status);
    }

    public static UpdateActivityCommand ToUpdateCommand(this ActivityFormViewModel model, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clock);

        var date = model.ActivityDate ?? clock.Today;
        var time = model.ActivityTime ?? new TimeOnly(10, 0);
        var localDateTime = date.ToDateTime(time);
        var utc = clock.ToUtcFromBusinessLocal(localDateTime);

        var related = new List<RelatedRef>();
        if (model.CustomerId.HasValue && model.CustomerId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Customer, model.CustomerId.Value));
        }
        if (model.LeadId.HasValue && model.LeadId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Lead, model.LeadId.Value));
        }
        if (model.OpportunityId.HasValue && model.OpportunityId.Value > 0)
        {
            related.Add(new RelatedRef(RelatedType.Opportunity, model.OpportunityId.Value));
        }

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        return new UpdateActivityCommand(
            model.Type,
            model.Subject,
            string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            utc,
            related,
            string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId,
            model.Status,
            rowVersion);
    }

    public static ActivityFormViewModel FromDetail(ActivityDetail detail, IClock clock, IReadOnlyList<UserOption> assigneeOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(clock);

        var businessDate = clock.BusinessDateOf(detail.ActivityDateUtc);
        var time = TimeOnly.FromTimeSpan(detail.ActivityDateUtc.TimeOfDay);

        return new ActivityFormViewModel
        {
            Id = detail.Id,
            Type = detail.Type,
            Subject = detail.Subject,
            Description = detail.Description,
            ActivityDate = businessDate,
            ActivityTime = time,
            CustomerId = detail.CustomerId,
            CustomerName = detail.CustomerName,
            LeadId = detail.LeadId,
            LeadName = detail.LeadName,
            OpportunityId = detail.OpportunityId,
            OpportunityName = detail.OpportunityName,
            AssignedToId = detail.AssignedToId,
            Status = detail.Status,
            RowVersion = Convert.ToBase64String(detail.RowVersion),
            AssigneeOptions = assigneeOptions
        };
    }

    public static void ApplyActivityErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
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
                "Subject" => nameof(ActivityFormViewModel.Subject),
                "Description" => nameof(ActivityFormViewModel.Description),
                "ActivityDate" => nameof(ActivityFormViewModel.ActivityDate),
                "Type" => nameof(ActivityFormViewModel.Type),
                "AssignedToId" => nameof(ActivityFormViewModel.AssignedToId),
                "Related" => "Related",
                _ => string.Empty
            };

            modelState.AddModelError(key, error.Message);
        }
    }
}
