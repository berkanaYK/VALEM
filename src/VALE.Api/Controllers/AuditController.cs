using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.AuditPolicy)]
[Route("api/audit")]
public sealed class AuditController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Get(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 250);
        var query = db.AuditEntries.AsNoTracking().Include(x => x.User).Include(x => x.Branch)
            .Where(x => (x.Branch != null && x.Branch.CompanyId == currentUser.CompanyId) || (x.User != null && x.User.CompanyId == currentUser.CompanyId));
        if (currentUser.CanAccessAllBranches)
        {
            if (branchId.HasValue)
            {
                var allowed = await db.Branches.AnyAsync(x => x.Id == branchId.Value && x.CompanyId == currentUser.CompanyId, cancellationToken);
                if (!allowed) throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu şube firmanıza ait değil.");
                query = query.Where(x => x.BranchId == branchId.Value);
            }
        }
        else
        {
            var own = currentUser.ResolveBranchId(branchId);
            query = query.Where(x => x.BranchId == own);
        }
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action.Trim()));
        var rows = await query.OrderByDescending(x => x.OccurredAt).Take(limit).Select(x => new AuditEntryDto(
            x.Id, x.OccurredAt, x.UserId, x.User != null ? x.User.FullName : null, x.BranchId, x.Branch != null ? x.Branch.Name : null,
            x.Action, x.EntityType, x.EntityId, x.Detail, x.Success)).ToListAsync(cancellationToken);
        return Ok(rows);
    }
}
