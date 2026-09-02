using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class SystemController(
    ValeDbContext db,
    IValeEmailSender emailSender,
    FirebasePushSender pushSender,
    ILogger<SystemController> logger) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var connected = false;
        IReadOnlyList<string> applied = [];
        IReadOnlyList<string> pending = [];
        var activeSessions = 0;
        var activePushes = 0;
        var unreadNotifications = 0;
        var tenantAudits = 0;
        var platformAudits = 0;

        try
        {
            connected = await db.Database.CanConnectAsync(cancellationToken);
            if (connected)
            {
                applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
                pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                var now = DateTimeOffset.UtcNow;
                activeSessions = await db.DeviceSessions.AsNoTracking().CountAsync(
                    x => x.RevokedAt == null && x.ExpiresAt > now,
                    cancellationToken);
                activePushes = await db.PushRegistrations.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
                unreadNotifications = await db.Notifications.AsNoTracking().CountAsync(x => !x.IsRead, cancellationToken);
                tenantAudits = await db.AuditEntries.AsNoTracking().CountAsync(cancellationToken);
                platformAudits = await db.PlatformAuditEntries.AsNoTracking().CountAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Platform sistem görünümünde veritabanı durumu okunamadı.");
            connected = false;
        }

        return View(new PlatformSystemModel(
            connected,
            db.Database.ProviderName ?? "Bilinmiyor",
            applied,
            pending,
            emailSender.IsConfigured,
            pushSender.IsConfigured,
            activeSessions,
            activePushes,
            unreadNotifications,
            tenantAudits,
            platformAudits,
            DateTimeOffset.UtcNow));
    }
}
