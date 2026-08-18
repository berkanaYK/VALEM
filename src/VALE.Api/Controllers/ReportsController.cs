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
            .Where(x => x.BranchId == resolvedBranchId &&
                        ((x.EntryAt >= rangeFrom && x.EntryAt <= rangeTo) ||
                         (x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo)))
            .Select(x => new
            {
                x.EntryAt,
                x.ExitAt,
                x.Status,
                Brand = x.Vehicle.Brand
            })
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

        var entered = tickets.Where(x => x.EntryAt >= rangeFrom && x.EntryAt <= rangeTo).ToList();
        var delivered = tickets.Where(x =>
            x.Status == TicketStatus.Delivered &&
            x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo).ToList();
        var cancelled = tickets.Count(x =>
            x.Status == TicketStatus.Cancelled &&
            x.ExitAt.HasValue && x.ExitAt >= rangeFrom && x.ExitAt <= rangeTo);
        var revenue = payments.Sum(x => x.Amount);
        var averageMinutes = delivered.Count == 0
            ? 0d
            : delivered.Select(x => (x.ExitAt!.Value - x.EntryAt).TotalMinutes).DefaultIfEmpty(0).Average();

        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(method => new PaymentBreakdownDto(
                method,
                payments.Count(x => x.Method == method),
                payments.Where(x => x.Method == method).Sum(x => x.Amount)))
            .ToList();

        var reportOffset = rangeFrom.Offset;
        var startDay = DateOnly.FromDateTime(rangeFrom.ToOffset(reportOffset).Date);
        var endDay = DateOnly.FromDateTime(rangeTo.ToOffset(reportOffset).Date);
        var daily = new List<DailyReportPointDto>();
        for (var day = startDay; day <= endDay; day = day.AddDays(1))
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), reportOffset);
            var dayEnd = dayStart.AddDays(1);
            daily.Add(new DailyReportPointDto(
                day,
                entered.Count(x => x.EntryAt >= dayStart && x.EntryAt < dayEnd),
                delivered.Count(x => x.ExitAt >= dayStart && x.ExitAt < dayEnd),
                payments.Where(x => x.PaidAt >= dayStart && x.PaidAt < dayEnd).Sum(x => x.Amount)));
        }

        var hourly = Enumerable.Range(0, 24)
            .Select(hour => new HourlyReportPointDto(
                hour,
                delivered.Count(x => x.ExitAt!.Value.ToOffset(reportOffset).Hour == hour),
                payments.Where(x => x.PaidAt.ToOffset(reportOffset).Hour == hour).Sum(x => x.Amount)))
            .ToList();

        var topBrands = entered
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Brand) ? "Belirtilmemiş" : x.Brand!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new VehicleBrandReportDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Vehicles)
            .ThenBy(x => x.Brand)
            .Take(8)
            .ToList();

        return Ok(new ReportSummaryDto(
            rangeFrom,
            rangeTo,
            entered.Count,
            delivered.Count,
            cancelled,
            active,
            revenue,
            delivered.Count == 0 ? 0 : revenue / delivered.Count,
            averageMinutes,
            paymentBreakdown,
            daily,
            hourly,
            topBrands));
    }
}
