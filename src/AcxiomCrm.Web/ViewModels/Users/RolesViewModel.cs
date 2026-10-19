using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Users;

public sealed record PermissionMatrixRow(
    string Module,
    string Permission,
    string DisplayName,
    bool Admin,
    bool Manager,
    bool SalesExecutive);

public sealed class RolesViewModel
{
    public IReadOnlyList<RoleSummary> Roles { get; set; } = [];
    public IReadOnlyList<PermissionMatrixRow> Matrix { get; set; } = [];
}
