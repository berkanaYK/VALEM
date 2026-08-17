using System.Security.Claims;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum gerekli", "Kullanıcı oturumu bulunamadı.");

    public Guid UserId => Guid.TryParse(
        User.FindFirst("sub")?.Value,
        out var userId)
            ? userId
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı kimliği okunamadı.");

    public Guid? BranchId => Guid.TryParse(User.FindFirstValue("branch_id"), out var branchId)
        ? branchId
        : null;

    public bool IsAdmin => User.IsInRole(Roles.Admin);

    public Guid ResolveBranchId(Guid? requestedBranchId)
    {
        if (IsAdmin)
        {
            return requestedBranchId
                   ?? BranchId
                   ?? throw new ApiException(
                       StatusCodes.Status400BadRequest,
                       "Şube gerekli",
                       "Yönetici işlemi için bir şube seçilmelidir.");
        }

        if (BranchId is not { } assignedBranchId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "Şube yetkisi yok",
                "Kullanıcı hesabına bir şube atanmamış.");
        }

        if (requestedBranchId.HasValue && requestedBranchId.Value != assignedBranchId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "Şube yetkisi yok",
                "Bu şubenin kayıtlarına erişim yetkiniz bulunmuyor.");
        }

        return assignedBranchId;
    }

    public void EnsureBranchAccess(Guid branchId) => ResolveBranchId(branchId);
}
