using AcxiomCrm.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    [Route("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? HttpContext.TraceIdentifier;
        var model = new ErrorViewModel(correlationId, 500, "An unexpected error occurred. Please contact your administrator.");
        return View("Error", model);
    }

    [Route("/Error/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ErrorStatusCode(int code)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? HttpContext.TraceIdentifier;

        return code switch
        {
            404 => View("NotFound", new ErrorViewModel(correlationId, 404, "The requested page or record could not be found.")),
            403 => View("Forbidden", new ErrorViewModel(correlationId, 403, "You do not have permission to access this resource.")),
            _ => View("Error", new ErrorViewModel(correlationId, code, $"An error ({code}) occurred."))
        };
    }
}
