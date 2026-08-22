using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class AuditService(ValeDbContext db, IHttpContextAccessor httpContextAccessor, CurrentUserContext currentUser)
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
        var companyId = currentUser.TryCompanyId;
        if (branchId.HasValue)
        {
            var branchCompanyId = await db.Branches.AsNoTracking()
                .Where(x => x.Id == branchId.Value)
                .Select(x => (Guid?)x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Denetim kaydının şubesi bulunamadı.");
            if (companyId.HasValue && companyId.Value != branchCompanyId)
                throw new ApiException(StatusCodes.Status403Forbidden, "Firma kapsamı geçersiz", "Denetim kaydı farklı bir firmaya yazılamaz.");
            companyId ??= branchCompanyId;
        }
        if (userId.HasValue)
        {
            var userCompanyId = await db.Users.AsNoTracking()
                .Where(x => x.Id == userId.Value)
                .Select(x => x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (userCompanyId.HasValue && companyId.HasValue && userCompanyId.Value != companyId.Value)
                throw new ApiException(StatusCodes.Status403Forbidden, "Firma kapsamı geçersiz", "Denetim kaydındaki kullanıcı farklı bir firmaya ait.");
            companyId ??= userCompanyId;
        }
        if (!companyId.HasValue)
            throw new InvalidOperationException("Denetim kaydı için firma kapsamı belirlenemedi.");

        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        db.AuditEntries.Add(new AuditEntry
        {
            CompanyId = companyId.Value,
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
