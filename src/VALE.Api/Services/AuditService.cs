using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class AuditService(ValeDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public async Task RecordAsync(
        Guid? userId,
        Guid? branchId,
        string action,
        string entityType,
        string? entityId,
        string detail,
        bool success = true,
        CancellationToken cancellationToken = default)
    {
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = userId,
            BranchId = branchId,
            Action = Limit(action, 80),
            EntityType = Limit(entityType, 80),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : Limit(entityId, 80),
            Detail = Limit(string.IsNullOrWhiteSpace(detail) ? "İşlem tamamlandı." : detail, 1000),
            IpAddress = string.IsNullOrWhiteSpace(ip) ? null : Limit(ip, 64),
            Success = success,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
