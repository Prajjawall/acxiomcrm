using AcxiomCrm.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Policy = Permissions.DashboardView)]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
