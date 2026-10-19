using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Permissions.RolesView)]
public sealed class RolesController : Controller
{
    private readonly IUserAdminService _userAdminService;

    public RolesController(IUserAdminService userAdminService)
    {
        _userAdminService = userAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var roles = await _userAdminService.GetRolesAsync(ct);

        var matrix = new List<PermissionMatrixRow>();
        foreach (var perm in Permissions.All)
        {
            var parts = perm.Split('.');
            var module = parts.Length > 0 ? char.ToUpperInvariant(parts[0][0]) + parts[0][1..] : "General";
            var action = parts.Length > 1 ? char.ToUpperInvariant(parts[1][0]) + parts[1][1..] : perm;

            matrix.Add(new PermissionMatrixRow(
                Module: module,
                Permission: perm,
                DisplayName: action,
                Admin: RolePermissionMap.Has(Roles.Admin, perm),
                Manager: RolePermissionMap.Has(Roles.Manager, perm),
                SalesExecutive: RolePermissionMap.Has(Roles.SalesExecutive, perm)
            ));
        }

        var model = new RolesViewModel
        {
            Roles = roles,
            Matrix = matrix.OrderBy(m => m.Module).ThenBy(m => m.DisplayName).ToList()
        };

        return View(model);
    }
}
