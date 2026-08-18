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
public sealed class BranchesController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = db.Branches.AsNoTracking().Where(x => x.CompanyId == currentUser.CompanyId && x.IsActive);
        if (!currentUser.CanAccessAllBranches)
        {
            var branchId = currentUser.ResolveBranchId(null);
            query = query.Where(x => x.Id == branchId);
        }
        var branches = await query.OrderBy(x => x.Name)
            .Select(x => new BranchDto(x.Id, x.Code, x.Name, x.City, x.Address, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(branches);
    }
}
