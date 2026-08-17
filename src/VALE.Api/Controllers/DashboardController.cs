using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.StaffPolicy)]
[Route("api/dashboard")]
public sealed class DashboardController(TicketService ticketService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        return Ok(await ticketService.GetDashboardAsync(branchId, cancellationToken));
    }
}

