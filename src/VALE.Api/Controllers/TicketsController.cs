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
    [ProducesResponseType<PagedResponse<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TicketSummaryDto>>> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        [FromQuery] TicketStatus? status,
        [FromQuery] bool includeClosed = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await ticketService.QueryAsync(
            branchId,
            search,
            status,
            includeClosed,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Valet},{Roles.Cashier}")]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketSummaryDto>> Create(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateAsync(request, cancellationToken);
        return Created($"/api/tickets/{ticket.Id}", ticket);
    }

    [HttpPatch("{ticketId:guid}/status")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Valet}")]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketSummaryDto>> UpdateStatus(
        Guid ticketId,
        UpdateTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await ticketService.UpdateStatusAsync(ticketId, request.Status, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/checkout")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Cashier}")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        Guid ticketId,
        CheckoutTicketRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await ticketService.CheckoutAsync(ticketId, request.PaymentMethod, cancellationToken));
    }
}

