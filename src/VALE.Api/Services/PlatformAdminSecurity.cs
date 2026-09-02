using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public static class PlatformAdminSecurity
{
    public const string CookieScheme = "VALEM.PlatformAdmin";
    public const string AreaName = "PlatformAdmin";
}

public sealed class PlatformAuditService(ValeDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public async Task RecordAsync(string action, string entityType, string? entityId, string detail, CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
            throw new InvalidOperationException("Platform denetim kaydı için yönetici oturumu gerekli.");

        var isPlatformAdmin = await db.UserRoles.AsNoTracking().AnyAsync(userRole =>
            userRole.UserId == adminUserId && db.Roles.Any(role =>
                role.Id == userRole.RoleId && role.Name == Roles.PlatformAdmin), cancellationToken);
        if (!isPlatformAdmin) throw new InvalidOperationException("Platform yöneticisi rolü doğrulanamadı.");

        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        db.PlatformAuditEntries.Add(new PlatformAuditEntry
        {
            AdminUserId = adminUserId,
            Action = Limit(action, 80),
            EntityType = Limit(entityType, 80),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : Limit(entityId, 80),
            Detail = Limit(string.IsNullOrWhiteSpace(detail) ? "İşlem tamamlandı." : detail, 1500),
            IpAddress = string.IsNullOrWhiteSpace(ip) ? null : Limit(ip, 64),
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}
