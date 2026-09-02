using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Domain;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

[Area(PlatformAdminSecurity.AreaName)]
public sealed class AccountController(UserManager<AppUser> userManager) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(Roles.PlatformAdmin))
            return RedirectToAction("Index", "Dashboard", new { area = PlatformAdminSecurity.AreaName });
        return View(new PlatformLoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(PlatformLoginModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive || user.CompanyId.HasValue || user.BranchId.HasValue ||
            !await userManager.IsInRoleAsync(user, Roles.PlatformAdmin))
        {
            await DelayFailedLoginAsync();
            ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
            return View(model);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Hesap geçici olarak kilitli. Bir süre sonra yeniden deneyin.");
            return View(model);
        }

        if (!await userManager.CheckPasswordAsync(user, model.Password))
        {
            await userManager.AccessFailedAsync(user);
            ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
            return View(model);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, Roles.PlatformAdmin),
            new Claim("security_stamp", user.SecurityStamp ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, PlatformAdminSecurity.CookieScheme);
        await HttpContext.SignInAsync(
            PlatformAdminSecurity.CookieScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Dashboard", new { area = PlatformAdminSecurity.AreaName });
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = PlatformAdminSecurity.CookieScheme, Roles = Roles.PlatformAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(PlatformAdminSecurity.CookieScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private static Task DelayFailedLoginAsync() => Task.Delay(Random.Shared.Next(120, 260));
}
