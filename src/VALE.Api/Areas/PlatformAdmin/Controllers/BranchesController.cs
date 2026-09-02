using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class BranchesController(ValeDbContext db, PlatformAuditService audit) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var query = db.Branches.AsNoTracking().Include(x => x.Company).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Code.ToLower().Contains(term) ||
                x.Company.Name.ToLower().Contains(term) ||
                x.City.ToLower().Contains(term));
        }

        ViewBag.Search = search;
        var rows = await query.OrderByDescending(x => x.IsActive).ThenBy(x => x.Company.Name).ThenBy(x => x.Name)
            .Take(250)
            .Select(branch => new PlatformBranchRow(
                branch.Id,
                branch.CompanyId,
                branch.Company.Name,
                branch.Code,
                branch.Name,
                branch.City,
                branch.InviteCode,
                branch.IsActive,
                db.ParkingTickets.Count(ticket => ticket.BranchId == branch.Id &&
                    ticket.Status != TicketStatus.Delivered && ticket.Status != TicketStatus.Cancelled)))
            .ToListAsync(cancellationToken);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildEditModelAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PlatformBranchEditModel model, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.Include(x => x.Company).SingleOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (branch is null) return NotFound();
        var code = model.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(x => x.Id != branch.Id && x.CompanyId == branch.CompanyId && x.Code == code, cancellationToken))
            ModelState.AddModelError(nameof(model.Code), "Bu şube kodu firma içinde kullanılıyor.");

        var activeTickets = await db.ParkingTickets.CountAsync(x => x.BranchId == branch.Id &&
            x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
        if (branch.IsActive && !model.IsActive && activeTickets > 0)
            ModelState.AddModelError(nameof(model.IsActive), $"Şubede {activeTickets} açık vale kaydı var. Önce kayıtları kapatın veya başka şubeye taşıyın.");

        if (!ModelState.IsValid)
        {
            await PopulateEditMetadataAsync(model, branch.CompanyId, cancellationToken);
            return View(model);
        }

        var before = $"{branch.Company.Name} / {branch.Code} - {branch.Name}, aktif={branch.IsActive}";
        branch.Code = code;
        branch.Name = model.Name.Trim();
        branch.City = model.City.Trim();
        branch.Address = model.Address?.Trim() ?? string.Empty;
        branch.IsActive = model.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("branch.updated", "Branch", branch.Id.ToString(),
            $"{before} → {branch.Code} - {branch.Name}, aktif={branch.IsActive}", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Branches", "Şube bilgileri güncellendi.", new { id = branch.Id });
    }

    [HttpPost]
    public async Task<IActionResult> RegenerateInvite(Guid id, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.Include(x => x.Company).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (branch is null) return NotFound();
        branch.InviteCode = GenerateInviteCode(branch.Company.Code, branch.Code);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("branch.invite.regenerated", "Branch", branch.Id.ToString(),
            $"{branch.Company.Name} / {branch.Name} davet kodu yenilendi; önceki kod geçersiz kılındı.", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Branches", "Davet kodu yenilendi; eski kod artık kullanılamaz.", new { id = branch.Id });
    }

    private async Task<PlatformBranchEditModel?> BuildEditModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await db.Branches.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PlatformBranchEditModel
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                CompanyName = x.Company.Name,
                Code = x.Code,
                Name = x.Name,
                City = x.City,
                Address = x.Address,
                IsActive = x.IsActive,
                InviteCode = x.InviteCode
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (model is not null) await PopulateEditMetadataAsync(model, model.CompanyId, cancellationToken);
        return model;
    }

    private async Task PopulateEditMetadataAsync(PlatformBranchEditModel model, Guid companyId, CancellationToken cancellationToken)
    {
        model.CompanyId = companyId;
        model.CompanyName = await db.Companies.AsNoTracking().Where(x => x.Id == companyId)
            .Select(x => x.Name).SingleAsync(cancellationToken);
        model.UserCount = await db.Users.AsNoTracking().CountAsync(x => x.BranchId == model.Id, cancellationToken);
        model.ActiveTicketCount = await db.ParkingTickets.AsNoTracking().CountAsync(x => x.BranchId == model.Id &&
            x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
        model.InviteCode = await db.Branches.AsNoTracking().Where(x => x.Id == model.Id)
            .Select(x => x.InviteCode).SingleOrDefaultAsync(cancellationToken);
    }

    private static string GenerateInviteCode(string companyCode, string branchCode)
    {
        var prefix = $"{companyCode}-{branchCode}";
        var value = $"{prefix}-{Guid.NewGuid():N}".ToUpperInvariant();
        return value[..Math.Min(40, prefix.Length + 9)];
    }
}
