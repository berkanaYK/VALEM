using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.StaffPolicy)]
[Route("api/tickets")]
public sealed class TicketsController(TicketService ticketService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TicketSummaryDto>>> GetAll([FromQuery] Guid? branchId, [FromQuery] string? search, [FromQuery] TicketStatus? status, [FromQuery] bool includeClosed = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await ticketService.QueryAsync(branchId, search, status, includeClosed, page, pageSize, cancellationToken));

    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailDto>> GetOne(Guid ticketId, CancellationToken cancellationToken) => Ok(await ticketService.GetDetailAsync(ticketId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Roles.OperationWritePolicy)]
    public async Task<ActionResult<TicketSummaryDto>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateAsync(request, cancellationToken);
        return Created($"/api/tickets/{ticket.Id}", ticket);
    }

    [HttpPut("{ticketId:guid}")]
    [Authorize(Policy = Roles.RecordsEditPolicy)]
    public async Task<ActionResult<TicketSummaryDto>> UpdateDetails(Guid ticketId, UpdateTicketDetailsRequest request, CancellationToken cancellationToken) =>
        Ok(await ticketService.UpdateDetailsAsync(ticketId, request, cancellationToken));

    [HttpDelete("{ticketId:guid}")]
    [Authorize(Policy = Roles.DeleteRecordsPolicy)]
    public async Task<IActionResult> Delete(Guid ticketId, [FromBody] DeleteTicketRequest request, CancellationToken cancellationToken)
    {
        await ticketService.DeleteAsync(ticketId, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{ticketId:guid}/status")]
    [Authorize(Policy = Roles.OperationWritePolicy)]
    public async Task<ActionResult<TicketSummaryDto>> UpdateStatus(Guid ticketId, UpdateTicketStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await ticketService.UpdateStatusAsync(ticketId, request.Status, cancellationToken));

    [HttpPost("{ticketId:guid}/checkout")]
    [Authorize(Policy = Roles.FinancePolicy)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(Guid ticketId, CheckoutTicketRequest request, CancellationToken cancellationToken) =>
        Ok(await ticketService.CheckoutAsync(ticketId, request.PaymentMethod, cancellationToken));
}
