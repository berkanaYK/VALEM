using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class DiagnosticsController(ValeDbContext db) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? traceId, Guid? companyId, CancellationToken ct)
    {
        var query = db.RequestFailures.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(traceId)) query = query.Where(x => x.TraceId == traceId);
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId);
        return View(await query.OrderByDescending(x => x.OccurredAt).Take(250).ToListAsync(ct));
    }
}
