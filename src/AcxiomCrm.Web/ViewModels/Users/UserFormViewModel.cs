using AcxiomCrm.Application.Users;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.Users;

public sealed class UserFormViewModel
{
    public string? Id { get; set; }

    [AxRequired("Full name")]
    public string FullName { get; set; } = string.Empty;

    [AxRequired("Username")]
    public string UserName { get; set; } = string.Empty;

    [AxRequired("Email")]
    [AxEmail]
    public string Email { get; set; } = string.Empty;

    [AxRequired("Role")]
    public string Role { get; set; } = string.Empty;

    public string? ManagerId { get; set; }

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<UserOption> ManagerOptions { get; set; } = [];
}
