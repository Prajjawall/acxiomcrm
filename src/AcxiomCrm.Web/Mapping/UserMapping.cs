using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.Mapping;

public static class UserMapping
{
    public static CreateUserCommand ToCreateCommand(this UserFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new CreateUserCommand(
            model.FullName,
            model.UserName,
            model.Email,
            model.Role,
            model.ManagerId,
            model.IsActive);
    }

    public static UpdateUserCommand ToUpdateCommand(this UserFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new UpdateUserCommand(
            model.FullName,
            model.UserName,
            model.Email,
            model.Role,
            model.ManagerId);
    }

    public static UserFormViewModel ToFormViewModel(this UserDetail detail, IReadOnlyList<UserOption> managerOptions)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new UserFormViewModel
        {
            Id = detail.Id,
            FullName = detail.FullName,
            UserName = detail.UserName,
            Email = detail.Email,
            Role = detail.Role,
            ManagerId = detail.ManagerId,
            IsActive = detail.IsActive,
            ManagerOptions = managerOptions
        };
    }

    public static void AddValidationErrors(this ModelStateDictionary modelState, IReadOnlyList<FieldError> errors)
    {
        foreach (var error in errors)
        {
            modelState.AddModelError(error.Field, error.Message);
        }
    }
}
