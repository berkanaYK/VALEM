using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class NotificationService(ValeDbContext db)
{
    public async Task CreateForUserAsync(
        Guid companyId,
        Guid userId,
        Guid? branchId,
        string title,
        string message,
        string type = "info",
        string? entityType = null,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(new UserNotification
        {
            CompanyId = companyId,
            UserId = userId,
            BranchId = branchId,
            Title = Limit(title, 160),
            Message = Limit(message, 1000),
            Type = Limit(type, 40),
            EntityType = string.IsNullOrWhiteSpace(entityType) ? null : Limit(entityType, 80),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : Limit(entityId, 80)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyApproversAsync(
        Guid companyId,
        Guid branchId,
        string title,
        string message,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var managerRoleIds = await db.Roles.AsNoTracking()
            .Where(x => x.Name != null && Roles.ManageUsersRoles.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var userIds = await db.UserRoles.AsNoTracking()
            .Where(x => managerRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var recipients = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id) && x.IsActive && x.CompanyId == companyId)
            .Select(x => new { x.Id, x.BranchId })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            db.Notifications.Add(new UserNotification
            {
                CompanyId = companyId,
                UserId = recipient.Id,
                BranchId = branchId,
                Title = Limit(title, 160),
                Message = Limit(message, 1000),
                Type = "approval",
                EntityType = entityType,
                EntityId = entityId
            });
        }

        if (recipients.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyBranchAsync(
        Guid companyId,
        Guid branchId,
        IEnumerable<string> roles,
        string title,
        string message,
        string type,
        string entityType,
        string entityId,
        Guid? exceptUserId = null,
        CancellationToken cancellationToken = default)
    {
        var roleNames = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roleIds = await db.Roles.AsNoTracking()
            .Where(x => x.Name != null && roleNames.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var candidateIds = await db.UserRoles.AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var recipients = await db.Users.AsNoTracking()
            .Where(x => candidateIds.Contains(x.Id) && x.IsActive && x.CompanyId == companyId && x.BranchId == branchId && (!exceptUserId.HasValue || x.Id != exceptUserId.Value))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in recipients)
        {
            db.Notifications.Add(new UserNotification
            {
                CompanyId = companyId,
                UserId = userId,
                BranchId = branchId,
                Title = Limit(title, 160),
                Message = Limit(message, 1000),
                Type = Limit(type, 40),
                EntityType = entityType,
                EntityId = entityId
            });
        }
        if (recipients.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
