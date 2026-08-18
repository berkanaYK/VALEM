using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.ReportsPolicy)]
[Route("api/reports")]
public sealed class ReportsController(ValeDbContext db, CurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("summary")]
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
            throw new ApiException(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz", "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (rangeTo - rangeFrom > TimeSpan.FromDays(366))
            throw new ApiException(StatusCodes.Status400BadRequest, "Tarih aralığı çok uzun", "Rapor en fazla 366 günlük aralık için alınabilir.");

        var resolvedBranchId = currentUser.ResolveBranchId(branchId);
        var tickets = await db.ParkingTickets.AsNoTracking()
            .Where(x => x.BranchId == resolvedBranchId &&
                        ((x.EntryAt >= rangeFrom && x.EntryAt <= rangeTo) ||
                         (x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo)))
            .Select(x => new { x.EntryAt, x.ExitAt, x.Status, Brand = x.Vehicle.Brand })
            .ToListAsync(cancellationToken);

        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.Ticket.BranchId == resolvedBranchId && x.PaidAt >= rangeFrom && x.PaidAt <= rangeTo)
            .Select(x => new { x.PaidAt, x.Method, x.Amount })
            .ToListAsync(cancellationToken);

        var active = await db.ParkingTickets.CountAsync(
            x => x.BranchId == resolvedBranchId && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled,
            cancellationToken);
        var entered = tickets.Where(x => x.EntryAt >= rangeFrom && x.EntryAt <= rangeTo).ToList();
        var delivered = tickets.Where(x => x.Status == TicketStatus.Delivered && x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo).ToList();
        var cancelled = tickets.Count(x => x.Status == TicketStatus.Cancelled && x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo);
        var revenue = payments.Sum(x => x.Amount);
        var averageMinutes = delivered.Count == 0 ? 0d : delivered.Select(x => (x.ExitAt!.Value - x.EntryAt).TotalMinutes).DefaultIfEmpty(0).Average();

        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(method => new PaymentBreakdownDto(method, payments.Count(x => x.Method == method), payments.Where(x => x.Method == method).Sum(x => x.Amount)))
            .ToList();

        var dayCount = Math.Clamp((rangeTo.Date - rangeFrom.Date).Days + 1, 1, 367);
        var daily = Enumerable.Range(0, dayCount)
            .Select(offset => rangeFrom.Date.AddDays(offset))
            .Select(day =>
            {
                var next = day.AddDays(1);
                return new DailyReportPointDto(
                    DateOnly.FromDateTime(day),
                    entered.Count(x => x.EntryAt >= day && x.EntryAt < next),
                    delivered.Count(x => x.ExitAt >= day && x.ExitAt < next),
                    payments.Where(x => x.PaidAt >= day && x.PaidAt < next).Sum(x => x.Amount));
            })
            .ToList();

        var hourly = Enumerable.Range(0, 24)
            .Select(hour => new HourlyReportPointDto(
                hour,
                delivered.Count(x => x.ExitAt?.Hour == hour),
                payments.Where(x => x.PaidAt.Hour == hour).Sum(x => x.Amount)))
            .ToList();

        var brands = entered
            .Where(x => !string.IsNullOrWhiteSpace(x.Brand))
            .GroupBy(x => x.Brand!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new VehicleBrandReportDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Vehicles)
            .ThenBy(x => x.Brand)
            .Take(10)
            .ToList();

        return Ok(new ReportSummaryDto(
            rangeFrom,
            rangeTo,
            entered.Count,
            delivered.Count,
            cancelled,
            active,
            revenue,
            delivered.Count == 0 ? 0m : revenue / delivered.Count,
            averageMinutes,
            paymentBreakdown,
            daily,
            hourly,
            brands));
    }
}
