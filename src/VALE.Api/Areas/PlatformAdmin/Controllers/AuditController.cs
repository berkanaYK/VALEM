using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class AuditController(ValeDbContext db) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var platformQuery = db.PlatformAuditEntries.AsNoTracking().Include(x => x.AdminUser).AsQueryable();
        var tenantQuery = db.AuditEntries.AsNoTracking().Include(x => x.Company).Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            platformQuery = platformQuery.Where(x =>
                x.Action.ToLower().Contains(term) || x.EntityType.ToLower().Contains(term) || x.Detail.ToLower().Contains(term));
            tenantQuery = tenantQuery.Where(x =>
                x.Action.ToLower().Contains(term) || x.EntityType.ToLower().Contains(term) ||
                x.Detail.ToLower().Contains(term) || x.Company.Name.ToLower().Contains(term));
        }

        ViewBag.Search = search;
        // İki sorgu aynı scoped DbContext üzerinde paralel çalıştırılamaz.
        var platformActions = await platformQuery.OrderByDescending(x => x.OccurredAt).Take(200)
            .Select(x => new PlatformAuditRow(
                x.OccurredAt, x.AdminUser.FullName, x.Action, x.EntityType, x.EntityId, x.Detail, x.IpAddress))
            .ToListAsync(cancellationToken);
        var tenantActions = await tenantQuery.OrderByDescending(x => x.OccurredAt).Take(200)
            .Select(x => new TenantAuditRow(
                x.OccurredAt, x.Company.Name, x.User == null ? null : x.User.FullName, x.Action,
                x.EntityType, x.EntityId, x.Detail, x.Success, x.IpAddress))
            .ToListAsync(cancellationToken);
        return View(new PlatformAuditIndexModel(platformActions, tenantActions));
    }
}
