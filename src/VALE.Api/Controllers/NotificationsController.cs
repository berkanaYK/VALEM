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
[Route("api/notifications")]
public sealed class NotificationsController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.Notifications.AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(limit).ToListAsync(cancellationToken);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> SetRead(Guid id, MarkNotificationReadRequest request, CancellationToken cancellationToken)
    {
        var row = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Bildirim bulunamadı", "Bildirim bulunamadı veya hesabınıza ait değil.");
        row.IsRead = request.IsRead;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        var rows = await db.Notifications
            .Where(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId && !x.IsRead)
            .ToListAsync(cancellationToken);
        foreach (var row in rows) row.IsRead = true;
        if (rows.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static NotificationDto Map(ValeNotification x) => new(x.Id, x.Title, x.Body, x.Type, x.IsRead, x.CreatedAt, x.BranchId);
}
