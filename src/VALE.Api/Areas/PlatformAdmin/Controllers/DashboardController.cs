using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class DashboardController(ValeDbContext db) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        // EF Core DbContext aynı anda birden fazla sorguyu desteklemez. Bu özet sorgularını
        // sırayla çalıştırmak, panel açılışında "second operation" hatasını önler.
        var companies = await db.Companies.AsNoTracking().CountAsync(cancellationToken);
        var activeCompanies = await db.Companies.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
        var users = await db.Users.AsNoTracking().CountAsync(x => x.CompanyId != null, cancellationToken);
        var activeUsers = await db.Users.AsNoTracking().CountAsync(x => x.CompanyId != null && x.IsActive, cancellationToken);
        var premiumUsers = await db.UserEntitlements.AsNoTracking().CountAsync(x => x.IsActive && x.Plan == "PremiumLifetime", cancellationToken);
        var branches = await db.Branches.AsNoTracking().CountAsync(cancellationToken);
        var activeTickets = await db.ParkingTickets.AsNoTracking().CountAsync(x =>
            x.Status != VALE.Contracts.TicketStatus.Delivered && x.Status != VALE.Contracts.TicketStatus.Cancelled,
            cancellationToken);
        var pending = await db.RegistrationRequests.AsNoTracking().CountAsync(x => x.Status == "Pending", cancellationToken);
        var payments = await db.Payments.AsNoTracking().Where(x => x.PaidAt >= since)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var actions = await db.PlatformAuditEntries.AsNoTracking().Include(x => x.AdminUser)
            .OrderByDescending(x => x.OccurredAt).Take(12)
            .Select(x => new PlatformAuditRow(x.OccurredAt, x.AdminUser.FullName, x.Action, x.EntityType, x.EntityId, x.Detail, x.IpAddress))
            .ToListAsync(cancellationToken);

        return View(new PlatformDashboardModel(
            companies,
            activeCompanies,
            users,
            activeUsers,
            premiumUsers,
            branches,
            activeTickets,
            pending,
            payments,
            actions));
    }
}
