using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController, Route("api/demo")]
public sealed class DemoController(ValeDbContext db, UserManager<AppUser> users, TokenService tokens) : ControllerBase
{
    [HttpPost, AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Start(CancellationToken ct)
    {
        var user = await db.Users.Include(x => x.Branch).Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == DemoData.UserId, ct);
        if (user is null || !user.IsActive || user.Company?.IsDemo != true || !user.Company.IsActive)
            throw new ApiException(503, "Deneme hazırlanıyor", "Deneme ekranı şu anda kullanılamıyor. Biraz sonra tekrar deneyin.");
        var roles = await users.GetRolesAsync(user);
        var token = tokens.Create(user, roles);
        return Ok(new LoginResponse(token.Value, token.ExpiresAt,
            new UserDto(user.Id, user.FullName, user.Email!, user.BranchId, user.Branch?.Name, roles.ToArray())));
    }
}
