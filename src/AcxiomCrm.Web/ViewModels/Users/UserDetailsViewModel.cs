using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Users;

public sealed class UserDetailsViewModel
{
    public UserDetail User { get; set; } = default!;
    public IReadOnlyList<UserOption> ReassignTargets { get; set; } = [];
}
