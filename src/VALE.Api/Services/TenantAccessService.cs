using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class TenantAccessService(ValeDbContext db, CurrentUserContext currentUser)
{
    public IQueryable<Branch> AccessibleBranches(bool activeOnly = true)
    {
        var companyId = currentUser.CompanyId;
        var query = db.Branches.Where(x => x.CompanyId == companyId);
        if (activeOnly) query = query.Where(x => x.IsActive);
        if (currentUser.CanAccessAllBranches) return query;

        var userId = currentUser.UserId;
        var primaryBranchId = currentUser.BranchId;
        return query.Where(branch =>
            (primaryBranchId.HasValue && branch.Id == primaryBranchId.Value) ||
            db.UserBranchMemberships.Any(membership =>
                membership.CompanyId == companyId &&
                membership.UserId == userId &&
                membership.BranchId == branch.Id &&
                membership.IsActive));
    }

    public async Task<Guid> ResolveBranchIdAsync(
        Guid? requestedBranchId,
        CancellationToken cancellationToken,
        bool activeOnly = true)
    {
        var branchId = requestedBranchId ?? currentUser.BranchId
            ?? throw new ApiException(StatusCodes.Status400BadRequest, "Şube gerekli", "İşlem için erişebildiğiniz bir şube seçin.");

        var allowed = await AccessibleBranches(activeOnly)
            .AsNoTracking()
            .AnyAsync(x => x.Id == branchId, cancellationToken);
        if (!allowed)
            throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Şube bulunamadı veya bu şube grubuna erişiminiz yok.");
        return branchId;
    }

    public async Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken, bool activeOnly = true) =>
        _ = await ResolveBranchIdAsync(branchId, cancellationToken, activeOnly);
}
