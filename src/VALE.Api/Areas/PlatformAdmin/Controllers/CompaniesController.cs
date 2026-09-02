using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class CompaniesController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    PlatformAuditService audit) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var query = db.Companies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") || EF.Functions.ILike(x.Code, $"%{term}%"));
        }

        ViewBag.Search = search;
        var rows = await query.OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).Take(250)
            .Select(company => new PlatformCompanyRow(
                company.Id,
                company.Name,
                company.Code,
                company.IsActive,
                db.Branches.Count(branch => branch.CompanyId == company.Id),
                db.Users.Count(user => user.CompanyId == company.Id),
                db.ParkingTickets.Count(ticket => ticket.CompanyId == company.Id && ticket.Status != TicketStatus.Delivered && ticket.Status != TicketStatus.Cancelled),
                company.CreatedAt))
            .ToListAsync(cancellationToken);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await db.Companies.AsNoTracking().Where(x => x.Id == id)
            .Select(company => new PlatformCompanyEditModel
            {
                Id = company.Id,
                Name = company.Name,
                Code = company.Code,
                IsActive = company.IsActive,
                OwnerUserId = company.OwnerUserId,
                OwnerName = db.Users.Where(user => user.Id == company.OwnerUserId).Select(user => user.FullName).FirstOrDefault(),
                BranchCount = db.Branches.Count(branch => branch.CompanyId == company.Id),
                UserCount = db.Users.Count(user => user.CompanyId == company.Id),
                ActiveTicketCount = db.ParkingTickets.Count(ticket => ticket.CompanyId == company.Id && ticket.Status != TicketStatus.Delivered && ticket.Status != TicketStatus.Cancelled)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (model is null) return NotFound();

        await PopulateEditMetadataAsync(model, companyOwnerUserId: model.OwnerUserId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PlatformCompanyEditModel model, CancellationToken cancellationToken)
    {
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (company is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
            return View(model);
        }

        var activeTickets = await db.ParkingTickets.CountAsync(x => x.CompanyId == company.Id &&
            x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
        if (company.IsActive && !model.IsActive && activeTickets > 0)
        {
            ModelState.AddModelError(nameof(model.IsActive), $"Firmada {activeTickets} açık vale kaydı var. Firma pasife alınmadan önce kayıtları kapatın.");
            await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
            return View(model);
        }

        var code = model.Code.Trim().ToUpperInvariant();
        if (await db.Companies.AnyAsync(x => x.Id != company.Id && x.Code == code, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "Bu firma kodu başka bir firma tarafından kullanılıyor.");
            await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
            return View(model);
        }

        AppUser? selectedOwner = null;
        if (model.OwnerUserId.HasValue)
        {
            selectedOwner = await userManager.Users.SingleOrDefaultAsync(
                x => x.Id == model.OwnerUserId.Value && x.CompanyId == company.Id && x.IsActive,
                cancellationToken);
            if (selectedOwner is null)
            {
                ModelState.AddModelError(nameof(model.OwnerUserId), "Ana sahip aynı firmadaki aktif bir kullanıcı olmalıdır.");
                await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
                return View(model);
            }
        }
        else if (await db.Users.AsNoTracking().AnyAsync(x => x.CompanyId == company.Id, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.OwnerUserId), "Kullanıcısı bulunan bir firmanın ana sahibi olmalıdır.");
            await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
            return View(model);
        }

        var previousOwnerName = company.OwnerUserId.HasValue
            ? await db.Users.AsNoTracking().Where(x => x.Id == company.OwnerUserId.Value)
                .Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var before = $"{company.Name} ({company.Code}), aktif={company.IsActive}, sahip={previousOwnerName ?? "yok"}";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (selectedOwner is not null && !await userManager.IsInRoleAsync(selectedOwner, Roles.Owner))
        {
            var ownerRole = await userManager.AddToRoleAsync(selectedOwner, Roles.Owner);
            if (!ownerRole.Succeeded)
            {
                foreach (var error in ownerRole.Errors) ModelState.AddModelError(nameof(model.OwnerUserId), error.Description);
                await transaction.RollbackAsync(cancellationToken);
                await PopulateEditMetadataAsync(model, company.OwnerUserId, cancellationToken);
                return View(model);
            }
        }
        company.Name = model.Name.Trim();
        company.Code = code;
        company.IsActive = model.IsActive;
        company.OwnerUserId = selectedOwner?.Id;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("company.updated", "Company", company.Id.ToString(),
            $"{before} → {company.Name} ({company.Code}), aktif={company.IsActive}, sahip={selectedOwner?.FullName ?? "yok"}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RedirectWithMessage(nameof(Edit), nameof(CompaniesController).Replace("Controller", string.Empty), "Firma bilgileri güncellendi.", new { id = company.Id });
    }

    private async Task PopulateEditMetadataAsync(PlatformCompanyEditModel model, Guid? companyOwnerUserId, CancellationToken cancellationToken)
    {
        model.OwnerName = companyOwnerUserId.HasValue
            ? await db.Users.AsNoTracking().Where(x => x.Id == companyOwnerUserId.Value).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var ownerCandidates = await db.Users.AsNoTracking()
            .Where(x => x.CompanyId == model.Id && x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, Display = x.FullName + " · " + (x.Email ?? "e-posta yok") })
            .ToListAsync(cancellationToken);
        model.OwnerCandidateIds = ownerCandidates.Select(x => x.Id).ToList();
        model.OwnerCandidateNames = ownerCandidates.Select(x => x.Display).ToList();
        model.BranchCount = await db.Branches.AsNoTracking().CountAsync(x => x.CompanyId == model.Id, cancellationToken);
        model.UserCount = await db.Users.AsNoTracking().CountAsync(x => x.CompanyId == model.Id, cancellationToken);
        model.ActiveTicketCount = await db.ParkingTickets.AsNoTracking().CountAsync(x => x.CompanyId == model.Id &&
            x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
    }
}
