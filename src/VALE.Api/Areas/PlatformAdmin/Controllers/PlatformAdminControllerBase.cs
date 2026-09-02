using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VALE.Api.Domain;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

[Area(PlatformAdminSecurity.AreaName)]
[Authorize(AuthenticationSchemes = PlatformAdminSecurity.CookieScheme, Roles = Roles.PlatformAdmin)]
[AutoValidateAntiforgeryToken]
public abstract class PlatformAdminControllerBase : Controller
{
    protected IActionResult RedirectWithMessage(string action, string controller, string message, object? routeValues = null)
    {
        TempData["Success"] = message;
        return RedirectToAction(action, controller, routeValues)!;
    }
}
