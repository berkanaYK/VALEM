using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class TicketsController(ValeDbContext db, PlatformAuditService audit) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? state, CancellationToken cancellationToken)
    {
        var query = db.ParkingTickets.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Include(x => x.Vehicle)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.TicketNumber.ToLower().Contains(term) ||
                x.Vehicle.LicensePlate.ToLower().Contains(term) ||
                x.Company.Name.ToLower().Contains(term));
        }
        query = state?.ToLowerInvariant() switch
        {
            "active" => query.Where(x => x.DeletedAt == null && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled),
            "closed" => query.Where(x => x.DeletedAt == null && (x.Status == TicketStatus.Delivered || x.Status == TicketStatus.Cancelled)),
            "deleted" => query.Where(x => x.DeletedAt != null),
            _ => query
        };

        ViewBag.Search = search;
        ViewBag.State = state;
        var rows = await query.OrderByDescending(x => x.EntryAt).Take(300)
            .Select(ticket => new PlatformTicketRow(
                ticket.Id,
                ticket.CompanyId,
                ticket.Company.Name,
                ticket.Branch.Name,
                ticket.TicketNumber,
                ticket.Vehicle.LicensePlate,
                ticket.Status,
                ticket.EntryAt,
                ticket.AmountDue,
                ticket.PaidAmount,
                ticket.DeletedAt != null))
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
    public async Task<IActionResult> Edit(PlatformTicketEditModel input, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets.IgnoreQueryFilters()
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (ticket is null) return NotFound();
        if (!Enum.IsDefined(input.Status)) ModelState.AddModelError(nameof(input.Status), "Vale durumu geçersiz.");

        var normalizedPlate = TextNormalizer.Plate(input.LicensePlate);
        if (normalizedPlate.Length < 3) ModelState.AddModelError(nameof(input.LicensePlate), "Geçerli bir plaka girin.");
        else if (await db.Vehicles.AnyAsync(x => x.CompanyId == ticket.CompanyId && x.Id != ticket.VehicleId && x.NormalizedPlate == normalizedPlate, cancellationToken))
            ModelState.AddModelError(nameof(input.LicensePlate), "Bu plaka firmanın başka bir araç kaydında kullanılıyor.");
        if (input.AmountDue < ticket.PaidAmount)
            ModelState.AddModelError(nameof(input.AmountDue), "Borç tutarı tahsil edilmiş tutarın altına indirilemez.");
        if (ticket.PaidAmount > 0 && input.Status != TicketStatus.Delivered)
            ModelState.AddModelError(nameof(input.Status), "Tahsilat bulunan kayıt teslim edildi durumundan yeniden açılamaz.");

        if (!ModelState.IsValid)
        {
            PopulateTicketMetadata(input, ticket);
            return View(input);
        }

        var before = $"{ticket.Vehicle.LicensePlate}, durum={ticket.Status}, saatlik={ticket.HourlyRate}, borç={ticket.AmountDue}";
        ticket.Vehicle.LicensePlate = input.LicensePlate.Trim().ToUpperInvariant();
        ticket.Vehicle.NormalizedPlate = normalizedPlate;
        ticket.Vehicle.Brand = Clean(input.Brand);
        ticket.Vehicle.Model = Clean(input.Model);
        ticket.Vehicle.Color = Clean(input.Color);
        ticket.KeyTag = Clean(input.KeyTag);
        ticket.ParkingSpot = Clean(input.ParkingSpot);
        ticket.Notes = Clean(input.Notes);
        ticket.HourlyRate = input.HourlyRate;
        ticket.AmountDue = input.AmountDue;
        ticket.Status = input.Status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        if (input.Status == TicketStatus.Requested) ticket.RequestedAt ??= DateTimeOffset.UtcNow;
        else if (input.Status is TicketStatus.Received or TicketStatus.Parked) ticket.RequestedAt = null;
        if (input.Status is TicketStatus.Delivered or TicketStatus.Cancelled) ticket.ExitAt ??= DateTimeOffset.UtcNow;
        else ticket.ExitAt = null;

        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("ticket.updated", "ParkingTicket", ticket.Id.ToString(),
            $"{ticket.Company.Name} / {ticket.TicketNumber}: {before} → {ticket.Vehicle.LicensePlate}, durum={ticket.Status}, saatlik={ticket.HourlyRate}, borç={ticket.AmountDue}",
            cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Tickets", "Vale kaydı güncellendi.", new { id = ticket.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets.IgnoreQueryFilters().Include(x => x.Vehicle).Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null) return NotFound();
        if (ticket.DeletedAt is not null)
            return RedirectWithMessage(nameof(Edit), "Tickets", "Kayıt zaten silinmiş durumda.", new { id });
        if (ticket.PaidAmount > 0 || ticket.Payments.Count > 0)
        {
            TempData["Error"] = "Tahsilat içeren kayıt mali bütünlük nedeniyle silinemez; not veya düzeltme kullanın.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
        {
            TempData["Error"] = "Silme nedeni en az 3 karakter olmalıdır.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        ticket.DeletedAt = DateTimeOffset.UtcNow;
        ticket.DeletedReason = reason.Trim();
        ticket.DeletedByUserId = null;
        ticket.UpdatedAt = ticket.DeletedAt;
        ticket.UpdatedByUserId = null;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("ticket.deleted", "ParkingTicket", ticket.Id.ToString(),
            $"{ticket.TicketNumber} / {ticket.Vehicle.LicensePlate} destek panelinden silindi. Neden: {ticket.DeletedReason}", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Tickets", "Kayıt geri alınabilir biçimde silindi.", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets.IgnoreQueryFilters().Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null) return NotFound();
        if (ticket.DeletedAt is null)
            return RedirectWithMessage(nameof(Edit), "Tickets", "Kayıt zaten aktif durumda.", new { id });

        var reason = ticket.DeletedReason;
        ticket.DeletedAt = null;
        ticket.DeletedByUserId = null;
        ticket.DeletedReason = null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedByUserId = null;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("ticket.restored", "ParkingTicket", ticket.Id.ToString(),
            $"{ticket.TicketNumber} / {ticket.Vehicle.LicensePlate} geri yüklendi. Önceki silme nedeni: {reason ?? "belirtilmemiş"}", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Tickets", "Silinen kayıt geri yüklendi.", new { id });
    }

    private async Task<PlatformTicketEditModel?> BuildEditModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Company).Include(x => x.Branch).Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null) return null;
        var model = new PlatformTicketEditModel
        {
            Id = ticket.Id,
            LicensePlate = ticket.Vehicle.LicensePlate,
            Brand = ticket.Vehicle.Brand,
            Model = ticket.Vehicle.Model,
            Color = ticket.Vehicle.Color,
            KeyTag = ticket.KeyTag,
            ParkingSpot = ticket.ParkingSpot,
            Notes = ticket.Notes,
            Status = ticket.Status,
            HourlyRate = ticket.HourlyRate,
            AmountDue = ticket.AmountDue,
            PaidAmount = ticket.PaidAmount,
            EntryAt = ticket.EntryAt,
            ExitAt = ticket.ExitAt,
            IsDeleted = ticket.DeletedAt is not null,
            DeletedReason = ticket.DeletedReason
        };
        PopulateTicketMetadata(model, ticket);
        return model;
    }

    private static void PopulateTicketMetadata(PlatformTicketEditModel model, ParkingTicket ticket)
    {
        model.CompanyId = ticket.CompanyId;
        model.CompanyName = ticket.Company.Name;
        model.BranchName = ticket.Branch.Name;
        model.TicketNumber = ticket.TicketNumber;
        model.PaidAmount = ticket.PaidAmount;
        model.EntryAt = ticket.EntryAt;
        model.ExitAt = ticket.ExitAt;
        model.IsDeleted = ticket.DeletedAt is not null;
        model.DeletedReason = ticket.DeletedReason;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
