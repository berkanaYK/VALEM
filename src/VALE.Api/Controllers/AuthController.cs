using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    TokenService tokenService,
    ValeDbContext db,
    IOptions<SeedOptions> seedOptions,
    PasswordResetCodeService resetCodes,
    OneTimeCodeService oneTimeCodes,
    IValeEmailSender emailSender,
    AuditService audit) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Email, cancellationToken);
        await ValidatePasswordAsync(user, request.Password);
        if (user.TwoFactorEnabled) return TwoFactorRequired();
        return Ok(await CompleteLoginAsync(user, "password", cancellationToken));
    }

    [HttpPost("login/2fa")]
    [AllowAnonymous]
    [EnableRateLimiting("2fa")]
    public async Task<ActionResult<LoginResponse>> LoginWithTwoFactor(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Email, cancellationToken);
        await ValidatePasswordAsync(user, request.Password);
        if (!user.TwoFactorEnabled || !await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.Code)))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Kod doğrulanamadı", "Authenticator uygulamasındaki 6 haneli kodu kontrol edin.");
        return Ok(await CompleteLoginAsync(user, "password+totp", cancellationToken));
    }

    [HttpPost("email-code/request")]
    [AllowAnonymous]
    [EnableRateLimiting("email-code")]
    public async Task<IActionResult> RequestEmailLoginCode(EmailCodeRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta ile giriş hazır değil", "Sunucu e-posta ayarları tamamlanmadan bu giriş yöntemi kullanılamaz.");

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { IsActive: true } && !await userManager.IsLockedOutAsync(user))
        {
            var code = await oneTimeCodes.CreateAsync(user, "email-login");
            await emailSender.SendLoginCodeAsync(user.Email ?? request.Email.Trim(), user.FullName, code, cancellationToken);
        }
        else
        {
            await Task.Delay(200, cancellationToken);
        }
        return Accepted(new { message = "E-posta adresi kayıtlı ve aktifse 6 haneli giriş kodu gönderildi." });
    }

    [HttpPost("email-code/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("email-code")]
    public async Task<ActionResult<LoginResponse>> VerifyEmailLoginCode(EmailCodeVerifyRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Email, cancellationToken, hideNotFound: true);
        if (user.TwoFactorEnabled && string.IsNullOrWhiteSpace(request.TwoFactorCode)) return TwoFactorRequired();
        if (!await oneTimeCodes.ValidateAndConsumeAsync(user, "email-login", request.Code.Trim()))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Kod doğrulanamadı", "Giriş kodu hatalı veya süresi dolmuş.");
        if (user.TwoFactorEnabled && !await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.TwoFactorCode!)))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Kod doğrulanamadı", "Authenticator kodunu kontrol edin.");
        return Ok(await CompleteLoginAsync(user, user.TwoFactorEnabled ? "email+totp" : "email", cancellationToken));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresiyle bir hesap zaten bulunuyor.");

        var employeeCode = Clean(request.EmployeeCode)?.ToUpperInvariant();
        if (employeeCode is not null && await userManager.Users.AnyAsync(x => x.EmployeeCode == employeeCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Personel kodu kullanımda", "Bu personel kodu başka bir hesapta kayıtlı.");

        var requestedCode = Clean(request.BranchCode)?.ToUpperInvariant();
        var defaultCode = seedOptions.Value.DefaultBranchCode.Trim().ToUpperInvariant();
        var branch = await db.Branches.OrderByDescending(x => x.Code == (requestedCode ?? defaultCode)).ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.IsActive && (requestedCode == null || x.Code == requestedCode), cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Girilen şube kodunu kontrol edin veya yöneticinizden doğru şube kodunu isteyin.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.PhoneNumber),
            EmployeeCode = employeeCode,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = false
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest, "Hesap oluşturulamadı", string.Join(" ", result.Errors.Select(x => x.Description)));
        var roleResult = await userManager.AddToRoleAsync(user, Roles.Valet);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ApiException(StatusCodes.Status500InternalServerError, "Hesap hazırlanamadı", "Başlangıç rolü atanamadı.");
        }
        await audit.RecordAsync(user.Id, branch.Id, "account.registered", "User", user.Id.ToString(), "Yeni hesap oluşturuldu; yönetici onayı bekliyor.", cancellationToken: cancellationToken);
        return Created("/api/auth/register", new RegisterResponse("Hesabınız oluşturuldu. Yöneticiniz onayladığında giriş yapabilirsiniz.", true));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta servisi hazır değil", "Şifre sıfırlama e-postası için sunucu mail ayarları henüz tamamlanmamış.");
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var code = await resetCodes.CreateAsync(user);
            await emailSender.SendPasswordResetCodeAsync(user.Email ?? request.Email.Trim(), user.FullName, code, cancellationToken);
        }
        else await Task.Delay(200, cancellationToken);
        return Accepted(new { message = "E-posta adresi kayıtlıysa 6 haneli sıfırlama kodu gönderildi." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await resetCodes.ValidateAndConsumeAsync(user, request.Code.Trim()))
            throw new ApiException(StatusCodes.Status400BadRequest, "Kod geçersiz", "Sıfırlama kodu hatalı veya süresi dolmuş.");
        var identityToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, identityToken, request.NewPassword);
        if (!result.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest, "Parola değiştirilemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        return Ok(new { message = "Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return Ok(MapUser(user, await userManager.GetRolesAsync(user)));
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        user.FullName = request.FullName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Profil güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        await audit.RecordAsync(user.Id, user.BranchId, "profile.updated", "User", user.Id.ToString(), "Ad soyad bilgisi güncellendi.", cancellationToken: cancellationToken);
        return Ok(MapUser(user, await userManager.GetRolesAsync(user)));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<AccountProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return Ok(await MapProfileAsync(user));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<AccountProfileDto>> UpdateAccountProfile(UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        var theme = NormalizeChoice(request.PreferredTheme, ["System", "Light", "Dark"], "System");
        var accent = NormalizeChoice(request.AccentTheme, ["Blue", "Indigo", "Emerald", "Orange"], "Blue");
        var user = await GetCurrentUserAsync(cancellationToken);
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = Clean(request.PhoneNumber);
        user.PreferredTheme = theme;
        user.AccentTheme = accent;
        user.ProfileColor = request.ProfileColor.ToUpperInvariant();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Profil kaydedilemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        await audit.RecordAsync(user.Id, user.BranchId, "profile.preferences.updated", "User", user.Id.ToString(), "Profil ve görünüm tercihleri güncellendi.", cancellationToken: cancellationToken);
        return Ok(await MapProfileAsync(user));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Parola değiştirilemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        await audit.RecordAsync(user.Id, user.BranchId, "security.password.changed", "User", user.Id.ToString(), "Kullanıcı parolasını değiştirdi.", cancellationToken: cancellationToken);
        return Ok(new { message = "Parolanız başarıyla değiştirildi." });
    }

    [HttpGet("2fa/status")]
    [Authorize]
    public async Task<ActionResult<TwoFactorStatusDto>> TwoFactorStatus(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        var recovery = user.TwoFactorEnabled ? await userManager.CountRecoveryCodesAsync(user) : 0;
        return Ok(new TwoFactorStatusDto(user.TwoFactorEnabled, !string.IsNullOrWhiteSpace(key), recovery));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    [EnableRateLimiting("2fa")]
    public async Task<ActionResult<TwoFactorSetupDto>> SetupTwoFactor(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user.TwoFactorEnabled) throw new ApiException(StatusCodes.Status409Conflict, "2FA zaten açık", "İki adımlı doğrulama hesabınızda zaten etkin.");
        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user) ?? throw new ApiException(StatusCodes.Status500InternalServerError, "Anahtar oluşturulamadı", "Authenticator anahtarı oluşturulamadı.");
        var issuer = Uri.EscapeDataString("VALE");
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString());
        var uri = $"otpauth://totp/{issuer}:{account}?secret={key.Replace(" ", string.Empty)}&issuer={issuer}&digits=6";
        return Ok(new TwoFactorSetupDto(key, uri));
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    [EnableRateLimiting("2fa")]
    public async Task<ActionResult<TwoFactorRecoveryCodesDto>> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.Code)))
            throw new ApiException(StatusCodes.Status400BadRequest, "Kod doğrulanamadı", "Authenticator uygulamasındaki kodu kontrol edin.");
        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];
        await audit.RecordAsync(user.Id, user.BranchId, "security.2fa.enabled", "User", user.Id.ToString(), "İki adımlı doğrulama etkinleştirildi.", cancellationToken: cancellationToken);
        return Ok(new TwoFactorRecoveryCodesDto(codes));
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    [EnableRateLimiting("2fa")]
    public async Task<IActionResult> DisableTwoFactor(TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (!user.TwoFactorEnabled) return Ok(new { message = "İki adımlı doğrulama zaten kapalı." });
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.Code)))
            throw new ApiException(StatusCodes.Status400BadRequest, "Kod doğrulanamadı", "2FA'yı kapatmak için güncel Authenticator kodunu girin.");
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await audit.RecordAsync(user.Id, user.BranchId, "security.2fa.disabled", "User", user.Id.ToString(), "İki adımlı doğrulama kapatıldı.", cancellationToken: cancellationToken);
        return Ok(new { message = "İki adımlı doğrulama kapatıldı." });
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize]
    [EnableRateLimiting("2fa")]
    public async Task<ActionResult<TwoFactorRecoveryCodesDto>> RegenerateRecoveryCodes(TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (!user.TwoFactorEnabled || !await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.Code)))
            throw new ApiException(StatusCodes.Status400BadRequest, "Kod doğrulanamadı", "Yeni kurtarma kodları için güncel Authenticator kodunu girin.");
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];
        await audit.RecordAsync(user.Id, user.BranchId, "security.2fa.recovery.regenerated", "User", user.Id.ToString(), "2FA kurtarma kodları yenilendi.", cancellationToken: cancellationToken);
        return Ok(new TwoFactorRecoveryCodesDto(codes));
    }

    private async Task<AppUser> FindUserAsync(string email, CancellationToken cancellationToken, bool hideNotFound = false)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        var user = await userManager.Users.Include(x => x.Branch).SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            if (hideNotFound) throw new ApiException(StatusCodes.Status401Unauthorized, "Giriş doğrulanamadı", "Kod hatalı, süresi dolmuş veya hesap aktif değil.");
            throw new ApiException(StatusCodes.Status401Unauthorized, "Giriş başarısız", user is { IsActive: false } ? "Hesabınız yönetici onayı bekliyor veya devre dışı." : "E-posta adresi veya parola hatalı.");
        }
        if (await userManager.IsLockedOutAsync(user))
            throw new ApiException(423, "Hesap geçici olarak kilitli", "Çok sayıda hatalı deneme yapıldı. Bir süre sonra yeniden deneyin.");
        return user;
    }

    private async Task ValidatePasswordAsync(AppUser user, string password)
    {
        if (await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return;
        }
        await userManager.AccessFailedAsync(user);
        throw new ApiException(StatusCodes.Status401Unauthorized, "Giriş başarısız", "E-posta adresi veya parola hatalı.");
    }

    private async Task<LoginResponse> CompleteLoginAsync(AppUser user, string method, CancellationToken cancellationToken)
    {
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.Create(user, roles);
        await audit.RecordAsync(user.Id, user.BranchId, "security.login", "User", user.Id.ToString(), $"Giriş yöntemi: {method}", cancellationToken: cancellationToken);
        return new LoginResponse(token.Value, token.ExpiresAt, MapUser(user, roles));
    }

    private ActionResult<LoginResponse> TwoFactorRequired()
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "İki adımlı doğrulama gerekli", Detail = "2FA_REQUIRED" };
        problem.Extensions["code"] = "two_factor_required";
        return Unauthorized(problem);
    }

    private async Task<AppUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId)) throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı kimliği okunamadı.");
        return await userManager.Users.Include(x => x.Branch).SingleOrDefaultAsync(x => x.Id == parsedUserId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı hesabı bulunamadı veya devre dışı.");
    }

    private async Task<AccountProfileDto> MapProfileAsync(AppUser user) => new(
        user.Id, user.FullName, user.Email ?? string.Empty, user.PhoneNumber, user.EmployeeCode, user.JobTitle,
        user.BranchId, user.Branch?.Name, (await userManager.GetRolesAsync(user)).ToList(), user.CreatedAt, user.LastLoginAt,
        user.PreferredTheme, user.AccentTheme, user.ProfileColor, user.TwoFactorEnabled);

    private static UserDto MapUser(AppUser user, IEnumerable<string> roles) => new(user.Id, user.FullName, user.Email ?? string.Empty, user.BranchId, user.Branch?.Name, roles.ToList());
    private static string NormalizeCode(string code) => code.Replace(" ", string.Empty).Replace("-", string.Empty);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeChoice(string value, IReadOnlyCollection<string> allowed, string fallback) => allowed.FirstOrDefault(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? fallback;
}
