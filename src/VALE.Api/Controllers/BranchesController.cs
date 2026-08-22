using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.StaffPolicy)]
[Route("api/branches")]
public sealed class BranchesController(CurrentUserContext currentUser, TenantAccessService tenantAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(CancellationToken cancellationToken)
    {
        var branches = await tenantAccess.AccessibleBranches().AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new BranchDto(x.Id, x.Code, x.Name, x.City, x.Address, x.IsActive, currentUser.CanAccessAllBranches ? x.InviteCode : null))
            .ToListAsync(cancellationToken);
        return Ok(branches);
    }
}
