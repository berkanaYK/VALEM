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
[Route("api/reports")]
public sealed class ReportsController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<ReportSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportSummaryDto>> Summary(
        [FromQuery] Guid? branchId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rangeTo = to ?? now;
        var rangeFrom = from ?? rangeTo.AddDays(-7);
        if (rangeTo <= rangeFrom)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz", "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        }
        if (rangeTo - rangeFrom > TimeSpan.FromDays(366))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Tarih aralığı çok uzun", "Rapor en fazla 366 günlük aralık için alınabilir.");
        }

        var resolvedBranchId = currentUser.ResolveBranchId(branchId);
        var tickets = await db.ParkingTickets
            .AsNoTracking()
            .Where(x => x.BranchId == resolvedBranchId && x.EntryAt >= rangeFrom && x.EntryAt <= rangeTo)
            .Select(x => new { x.EntryAt, x.ExitAt, x.Status, x.PaidAmount })
            .ToListAsync(cancellationToken);

        var payments = await db.Payments
            .AsNoTracking()
            .Where(x => x.Ticket.BranchId == resolvedBranchId && x.PaidAt >= rangeFrom && x.PaidAt <= rangeTo)
            .Select(x => new { x.PaidAt, x.Method, x.Amount })
            .ToListAsync(cancellationToken);

        var active = await db.ParkingTickets.CountAsync(
            x => x.BranchId == resolvedBranchId &&
                 x.Status != TicketStatus.Delivered &&
                 x.Status != TicketStatus.Cancelled,
            cancellationToken);

        var delivered = tickets.Where(x => x.Status == TicketStatus.Delivered).ToList();
        var revenue = payments.Sum(x => x.Amount);
        var averageMinutes = delivered.Count == 0
            ? 0d
            : delivered.Where(x => x.ExitAt.HasValue)
                .Select(x => (x.ExitAt!.Value - x.EntryAt).TotalMinutes)
                .DefaultIfEmpty(0)
                .Average();

        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(method => new PaymentBreakdownDto(
                method,
                payments.Count(x => x.Method == method),
                payments.Where(x => x.Method == method).Sum(x => x.Amount)))
            .ToList();

        var startDay = DateOnly.FromDateTime(rangeFrom.UtcDateTime.Date);
        var endDay = DateOnly.FromDateTime(rangeTo.UtcDateTime.Date);
        var daily = new List<DailyReportPointDto>();
        for (var day = startDay; day <= endDay; day = day.AddDays(1))
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);
            daily.Add(new DailyReportPointDto(
                day,
                tickets.Count(x => x.EntryAt >= dayStart && x.EntryAt < dayEnd),
                tickets.Count(x => x.Status == TicketStatus.Delivered && x.ExitAt >= dayStart && x.ExitAt < dayEnd),
                payments.Where(x => x.PaidAt >= dayStart && x.PaidAt < dayEnd).Sum(x => x.Amount)));
        }

        return Ok(new ReportSummaryDto(
            rangeFrom,
            rangeTo,
            tickets.Count,
            delivered.Count,
            tickets.Count(x => x.Status == TicketStatus.Cancelled),
            active,
            revenue,
            delivered.Count == 0 ? 0 : revenue / delivered.Count,
            averageMinutes,
            paymentBreakdown,
            daily));
    }
}
