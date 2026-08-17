using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
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
                Detail = "E-posta adresi veya parola hatalı."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.Create(user, roles);
        return Ok(new LoginResponse(token.Value, token.ExpiresAt, MapUser(user, roles)));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized();
        }

        var user = await userManager.Users
            .Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == parsedUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(MapUser(user, roles));
    }

    private static UserDto MapUser(AppUser user, IReadOnlyCollection<string> roles) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.BranchId,
        user.Branch?.Name,
        roles.ToList());
}
