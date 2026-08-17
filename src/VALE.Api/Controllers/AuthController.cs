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
    IValeEmailSender emailSender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(request.Email.Trim());
        var user = await userManager.Users
            .Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Giriş başarısız",
                Detail = user is { IsActive: false }
                    ? "Hesabınız yönetici onayı bekliyor veya devre dışı."
                    : "E-posta adresi veya parola hatalı."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.Create(user, roles);
        return Ok(new LoginResponse(token.Value, token.ExpiresAt, MapUser(user, roles)));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresiyle bir hesap zaten bulunuyor.");
        }

        var branchCode = seedOptions.Value.DefaultBranchCode.Trim().ToUpperInvariant();
        var branch = await db.Branches
            .OrderByDescending(x => x.Code == branchCode)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Şube hazır değil", "Hesap oluşturmak için aktif bir VALE şubesi bulunamadı.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            BranchId = branch.Id,
            Branch = branch,
            IsActive = false
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Hesap oluşturulamadı", string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Valet);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ApiException(StatusCodes.Status500InternalServerError, "Hesap hazırlanamadı", "Kullanıcı rolü atanamadı.");
        }

        return Created("/api/auth/register", new RegisterResponse(
            "Hesabınız oluşturuldu. Güvenlik için ilk girişten önce yönetici onayı gerekir.",
            true));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
        {
            throw new ApiException(
                StatusCodes.Status503ServiceUnavailable,
                "E-posta servisi hazır değil",
                "Şifre sıfırlama e-postası için sunucu mail ayarları henüz tamamlanmamış.");
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var code = await resetCodes.CreateAsync(user);
            await emailSender.SendPasswordResetCodeAsync(user.Email ?? request.Email.Trim(), user.FullName, code, cancellationToken);
        }
        else
        {
            await Task.Delay(200, cancellationToken);
        }

        return Accepted(new { message = "E-posta adresi kayıtlıysa 6 haneli sıfırlama kodu gönderildi." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await resetCodes.ValidateAndConsumeAsync(user, request.Code.Trim()))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Kod geçersiz", "Sıfırlama kodu hatalı veya süresi dolmuş.");
        }

        var identityToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, identityToken, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Parola değiştirilemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        return Ok(new { message = "Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        return Ok(MapUser(user, roles));
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        user.FullName = request.FullName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Profil güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(MapUser(user, roles));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Parola değiştirilemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        return Ok(new { message = "Parolanız başarıyla değiştirildi." });
    }

    private async Task<AppUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı kimliği okunamadı.");
        }

        return await userManager.Users
            .Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == parsedUserId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı hesabı bulunamadı veya devre dışı.");
    }

    private static UserDto MapUser(AppUser user, IEnumerable<string> roles) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.BranchId,
        user.Branch?.Name,
        roles.ToList());
}
