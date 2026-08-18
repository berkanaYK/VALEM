using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor, ValeDbContext db)
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum gerekli", "Kullanıcı oturumu bulunamadı.");

    public Guid UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)
        ? userId
        : throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı kimliği okunamadı.");

    public Guid CompanyId => Guid.TryParse(User.FindFirstValue("company_id"), out var companyId)
        ? companyId
        : throw new ApiException(StatusCodes.Status401Unauthorized, "Firma oturumu geçersiz", "Oturumunuz bir firmaya bağlı değil. Yeniden giriş yapın.");

    public Guid? BranchId => Guid.TryParse(User.FindFirstValue("branch_id"), out var branchId) ? branchId : null;
    public IReadOnlyList<string> RoleNames => User.FindAll("role").Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public bool IsOwner => User.IsInRole(Roles.Owner);
    public bool IsAdmin => User.IsInRole(Roles.Admin) || IsOwner;
    public bool CanAccessAllBranches => Roles.CrossBranchRoles.Any(User.IsInRole);
    public bool CanViewReports => Roles.ReportRoles.Any(User.IsInRole);
    public bool CanViewFinancials => Roles.FinanceRoles.Any(User.IsInRole) || CanViewReports;

    public Guid ResolveBranchId(Guid? requestedBranchId)
    {
        if (CanAccessAllBranches)
        {
            return requestedBranchId ?? BranchId
                ?? throw new ApiException(StatusCodes.Status400BadRequest, "Şube gerekli", "İşlem için bir şube seçin.");
        }

        if (BranchId is not { } assignedBranchId)
            throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Hesabınıza bir şube atanmamış.");
        if (requestedBranchId.HasValue && requestedBranchId.Value != assignedBranchId)
            throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu şubenin kayıtlarına erişim yetkiniz bulunmuyor.");
        return assignedBranchId;
    }

    public async Task<Guid> ResolveBranchIdAsync(Guid? requestedBranchId, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(requestedBranchId);
        var belongsToCompany = await db.Branches.AsNoTracking().AnyAsync(
            x => x.Id == branchId && x.CompanyId == CompanyId && x.IsActive,
            cancellationToken);
        if (!belongsToCompany)
            throw new ApiException(StatusCodes.Status403Forbidden, "Firma sınırı", "Bu şube hesabınızın bağlı olduğu firmaya ait değil.");
        return branchId;
    }

    public void EnsureCompanyAccess(Guid companyId)
    {
        if (companyId != CompanyId)
            throw new ApiException(StatusCodes.Status403Forbidden, "Firma yetkisi yok", "Başka bir firmanın verilerine erişemezsiniz.");
    }

    public async Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var belongsToCompany = await db.Branches.AsNoTracking().AnyAsync(
            x => x.Id == branchId && x.CompanyId == CompanyId,
            cancellationToken);
        if (!belongsToCompany)
            throw new ApiException(StatusCodes.Status403Forbidden, "Firma sınırı", "Bu şube hesabınızın bağlı olduğu firmaya ait değil.");
        if (!CanAccessAllBranches && BranchId != branchId)
            throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu şubenin kayıtlarına erişim yetkiniz bulunmuyor.");
    }

    public void EnsureBranchAccess(Guid branchId)
    {
        if (CanAccessAllBranches) return;
        _ = ResolveBranchId(branchId);
    }
}
