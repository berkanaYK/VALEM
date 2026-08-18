using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationSummaryDto>> Get(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 250);
        var query = db.Notifications.AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId);
        var unread = await query.CountAsync(x => !x.IsRead, cancellationToken);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(limit)
            .Select(x => new NotificationDto(x.Id, x.Title, x.Message, x.Type, x.IsRead, x.CreatedAt, x.ReadAt, x.EntityType, x.EntityId))
            .ToListAsync(cancellationToken);
        return Ok(new NotificationSummaryDto(unread, rows));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await db.Notifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId,
            cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Bildirim bulunamadı", "Bildirim bulunamadı.");
        if (!row.IsRead)
        {
            row.IsRead = true;
            row.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var rows = await db.Notifications.Where(x => x.CompanyId == currentUser.CompanyId && x.UserId == currentUser.UserId && !x.IsRead)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows) { row.IsRead = true; row.ReadAt = now; }
        if (rows.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
