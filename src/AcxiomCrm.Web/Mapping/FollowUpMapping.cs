using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.FollowUps;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class FollowUpMapping
{
    public static CreateFollowUpCommand ToCreateCommand(this FollowUpFormViewModel model, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clock);

        var date = model.FollowUpDate ?? clock.Today;
        var time = model.FollowUpTime ?? new TimeOnly(10, 0);
        var localDateTime = date.ToDateTime(time);
        var utc = clock.ToUtcFromBusinessLocal(localDateTime);

        return new CreateFollowUpCommand(
            new RelatedRef(model.RelatedType, model.RelatedId),
            utc,
            model.Subject,
            model.Type,
            string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks,
            string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId,
            model.IsHistoricalEntry);
    }

    public static UpdateFollowUpCommand ToUpdateCommand(this FollowUpFormViewModel model, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clock);

        var date = model.FollowUpDate ?? clock.Today;
        var time = model.FollowUpTime ?? new TimeOnly(10, 0);
        var localDateTime = date.ToDateTime(time);
        var utc = clock.ToUtcFromBusinessLocal(localDateTime);

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        return new UpdateFollowUpCommand(
            utc,
            model.Subject,
            model.Type,
            string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks,
            string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId,
            rowVersion);
    }

    public static FollowUpFormViewModel FromDetail(FollowUpDetail detail, IClock clock, IReadOnlyList<UserOption> assigneeOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(clock);

        var businessDate = clock.BusinessDateOf(detail.FollowUpDateUtc);
        var time = TimeOnly.FromTimeSpan(detail.FollowUpDateUtc.TimeOfDay);

        return new FollowUpFormViewModel
        {
            Id = detail.Id,
            RelatedType = detail.RelatedType,
            RelatedId = detail.RelatedId,
            RelatedName = detail.RelatedName,
            FollowUpDate = businessDate,
            FollowUpTime = time,
            Subject = detail.Subject,
            Type = detail.Type,
            Remarks = detail.Remarks,
            AssignedToId = detail.AssignedToId,
            IsHistoricalEntry = detail.IsHistoricalEntry,
            RowVersion = Convert.ToBase64String(detail.RowVersion),
            AssigneeOptions = assigneeOptions
        };
    }

    public static void ApplyFollowUpErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
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
                "Subject" => nameof(FollowUpFormViewModel.Subject),
                "Remarks" => nameof(FollowUpFormViewModel.Remarks),
                "FollowUpDate" => nameof(FollowUpFormViewModel.FollowUpDate),
                "Type" => nameof(FollowUpFormViewModel.Type),
                "AssignedToId" => nameof(FollowUpFormViewModel.AssignedToId),
                "Related" => nameof(FollowUpFormViewModel.RelatedId),
                _ => string.Empty
            };

            modelState.AddModelError(key, error.Message);
        }
    }
}
