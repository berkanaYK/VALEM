using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.StaffPolicy)]
[Route("api/push")]
public sealed class PushController(
    ValeDbContext db,
    CurrentUserContext currentUser,
    FirebasePushSender pushSender) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<PushStatusDto>> Status(CancellationToken cancellationToken)
    {
        var count = await db.PushRegistrations.AsNoTracking()
            .CountAsync(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId && x.IsActive, cancellationToken);
        return Ok(new PushStatusDto(pushSender.IsConfigured, count));
    }

    [HttpGet("registrations")]
    public async Task<ActionResult<IReadOnlyList<PushRegistrationDto>>> Registrations(CancellationToken cancellationToken)
    {
        var rows = await db.PushRegistrations.AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new PushRegistrationDto(x.Id, x.Platform, x.DeviceName, x.LastSeenAt, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPut("register")]
    public async Task<ActionResult<PushRegistrationDto>> Register(PushRegistrationRequest request, CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform.Trim().ToLowerInvariant();
        var deviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? null : request.DeviceName.Trim();

        var registration = await db.PushRegistrations.SingleOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (registration is null)
        {
            registration = new PushRegistration
            {
                CompanyId = currentUser.CompanyId,
                UserId = currentUser.UserId,
                Token = token,
                Platform = platform,
                DeviceName = deviceName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow
            };
            db.PushRegistrations.Add(registration);
        }
        else
        {
            registration.CompanyId = currentUser.CompanyId;
            registration.UserId = currentUser.UserId;
            registration.Platform = platform;
            registration.DeviceName = deviceName;
            registration.IsActive = true;
            registration.LastSeenAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PushRegistrationDto(registration.Id, registration.Platform, registration.DeviceName, registration.LastSeenAt, registration.IsActive));
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(PushRegistrationRequest request, CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var registration = await db.PushRegistrations.SingleOrDefaultAsync(
            x => x.Token == token && x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId,
            cancellationToken);
        if (registration is not null)
        {
            registration.IsActive = false;
            registration.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<ActionResult<PushTestResponse>> Test(CancellationToken cancellationToken)
    {
        var result = await pushSender.SendToUsersAsync(
            currentUser.CompanyId,
            [currentUser.UserId],
            "VALE bildirim testi",
            "Gerçek push bildirimi başarıyla çalışıyor.",
            "PushTest",
            currentUser.BranchId,
            cancellationToken);
        return Ok(new PushTestResponse(pushSender.IsConfigured, result.Attempted, result.Delivered));
    }
}
