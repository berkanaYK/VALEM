using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class EmailVerificationController(
    UserManager<AppUser> userManager,
    ValeDbContext db,
    IValeEmailSender emailSender,
    AuditService audit) : ControllerBase
{
    [HttpPost("email-confirmation/resend")]
    [AllowAnonymous]
    [EnableRateLimiting("email-code")]
    public async Task<IActionResult> Resend(EmailCodeRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta servisi hazır değil", "E-posta doğrulama bağlantısı şu anda gönderilemiyor.");

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false })
            await SendConfirmationAsync(user, cancellationToken);
        else
            await Task.Delay(200, cancellationToken);

        return Accepted(new { message = "E-posta adresi kayıtlı ve henüz doğrulanmamışsa yeni doğrulama bağlantısı gönderildi." });
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<ContentResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Html(StatusCodes.Status400BadRequest, "Bağlantı geçersiz", "Bu doğrulama bağlantısına ait hesap bulunamadı.");

        if (user.EmailConfirmed)
            return Html(StatusCodes.Status200OK, "E-posta zaten doğrulandı", "E-posta adresiniz daha önce doğrulanmış. VALE uygulamasına dönebilirsiniz.");

        string identityToken;
        try
        {
            identityToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            return Html(StatusCodes.Status400BadRequest, "Bağlantı geçersiz", "Doğrulama bağlantısı bozuk veya geçersiz.");
        }

        var confirmed = await userManager.ConfirmEmailAsync(user, identityToken);
        if (!confirmed.Succeeded)
            return Html(StatusCodes.Status400BadRequest, "Bağlantı kullanılamadı", "Doğrulama bağlantısının süresi dolmuş veya bağlantı daha önce kullanılmış olabilir.");

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Owner, StringComparer.OrdinalIgnoreCase))
        {
            user.IsActive = true;
        }
        else
        {
            var registration = await db.RegistrationRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ApplicantUserId == user.Id, cancellationToken);
            user.IsActive = registration?.Status == "Approved";
        }

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return Html(StatusCodes.Status500InternalServerError, "Doğrulama tamamlanamadı", "E-posta doğrulandı ancak hesap durumu güncellenemedi. Destek ekibiyle iletişime geçin.");

        await userManager.UpdateSecurityStampAsync(user);
        await audit.RecordAsync(user.Id, user.BranchId, "security.email.confirmed", "User", user.Id.ToString(), "E-posta sahipliği doğrulama bağlantısıyla onaylandı.", cancellationToken: cancellationToken);

        var message = user.IsActive
            ? "E-posta adresiniz doğrulandı. Artık VALE uygulamasına dönüp seçtiğiniz giriş yöntemiyle giriş yapabilirsiniz."
            : "E-posta adresiniz doğrulandı. Personel hesabınız yönetici onayından sonra girişe açılacak.";
        return Html(StatusCodes.Status200OK, "E-posta doğrulandı", message);
    }

    private async Task SendConfirmationAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;
        var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(scheme)) scheme = Request.Scheme;
        var url = $"{scheme}://{Request.Host}/api/auth/confirm-email?userId={user.Id:D}&token={Uri.EscapeDataString(encodedToken)}";
        await emailSender.SendEmailConfirmationLinkAsync(user.Email, user.FullName, url, cancellationToken);
    }

    private ContentResult Html(int status, string title, string message)
    {
        static string E(string value) => System.Net.WebUtility.HtmlEncode(value);
        return new ContentResult
        {
            StatusCode = status,
            ContentType = "text/html; charset=utf-8",
            Content = $$"""
                <!doctype html>
                <html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>{{E(title)}} • VALE</title>
                <style>body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:#f4f6fb;color:#172033;margin:0;padding:32px}main{max-width:560px;margin:8vh auto;background:#fff;border-radius:24px;padding:32px;box-shadow:0 10px 35px #00000012}h1{margin-top:0}p{line-height:1.6}.ok{font-weight:700;color:#2563eb}</style></head>
                <body><main><div class="ok">VALE</div><h1>{{E(title)}}</h1><p>{{E(message)}}</p><p>Bu sayfayı kapatıp VALE uygulamasına dönebilirsiniz.</p></main></body></html>
                """
        };
    }
}
