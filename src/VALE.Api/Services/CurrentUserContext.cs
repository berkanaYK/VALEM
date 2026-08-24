using System.Security.Claims;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum gerekli", "Kullanıcı oturumu bulunamadı.");

    public Guid? TryUserId => User.Identity?.IsAuthenticated == true && Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) ? userId : null;
    public Guid UserId => TryUserId is { } userId
        ? userId
        : throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum geçersiz", "Kullanıcı kimliği okunamadı.");

    public Guid? TryCompanyId => User.Identity?.IsAuthenticated == true && Guid.TryParse(User.FindFirstValue("company_id"), out var companyId) ? companyId : null;
    public Guid CompanyId => TryCompanyId is { } companyId
        ? companyId
        : throw new ApiException(StatusCodes.Status401Unauthorized, "Oturum güncel değil", "Firma kapsamı bulunamadı. Lütfen yeniden giriş yapın.");

    public Guid? BranchId => Guid.TryParse(User.FindFirstValue("branch_id"), out var branchId) ? branchId : null;
    public IReadOnlyList<string> RoleNames => User.FindAll("role").Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public bool IsOwner => User.IsInRole(Roles.Owner);
    public bool IsAdmin => User.IsInRole(Roles.Admin) || IsOwner;
    public bool CanAccessAllBranches => Roles.CrossBranchRoles.Any(User.IsInRole);
    public bool CanViewReports => Roles.ReportRoles.Any(User.IsInRole);
    public bool CanViewFinancials => Roles.FinanceRoles.Any(User.IsInRole) || CanViewReports;

}
