using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class RegistrationsController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    PlatformAuditService audit,
    FirebasePushSender pushSender) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status = "Pending", CancellationToken cancellationToken = default)
    {
        var query = db.RegistrationRequests.AsNoTracking()
            .Include(x => x.ApplicantUser)
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.ApplicantUser.FullName.ToLower().Contains(term) ||
                (x.ApplicantUser.Email != null && x.ApplicantUser.Email.ToLower().Contains(term)) ||
                x.Company.Name.ToLower().Contains(term) || x.Branch.Name.ToLower().Contains(term));
        }

        ViewBag.Search = search;
        ViewBag.Status = status;
        var rows = await query.OrderBy(x => x.Status == "Pending" ? 0 : 1).ThenByDescending(x => x.CreatedAt).Take(250)
            .Select(x => new PlatformRegistrationRow(
                x.Id, x.ApplicantUserId, x.ApplicantUser.FullName, x.ApplicantUser.Email ?? string.Empty,
                x.ApplicantUser.EmailConfirmed, x.CompanyId, x.Company.Name, x.Branch.Name, x.RequestedRole,
                x.Status, x.CreatedAt, x.ReviewedAt, x.Note))
            .ToListAsync(cancellationToken);
        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Decide(Guid id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var registration = await db.RegistrationRequests
            .Include(x => x.ApplicantUser).Include(x => x.Company).Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (registration is null) return NotFound();
        if (registration.Status != "Pending")
        {
            TempData["Error"] = "Bu başvuru daha önce sonuçlandırılmış.";
            return RedirectToAction(nameof(Index));
        }
        if (registration.ApplicantUser.CompanyId != registration.CompanyId ||
            registration.ApplicantUser.BranchId != registration.BranchId ||
            registration.Branch.CompanyId != registration.CompanyId)
            throw new InvalidOperationException("Başvuru, kullanıcı ve şube firma kapsamı eşleşmiyor.");

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote?.Length > 500)
        {
            TempData["Error"] = "Başvuru notu en fazla 500 karakter olabilir.";
            return RedirectToAction(nameof(Index));
        }

        registration.Status = approve ? "Approved" : "Rejected";
        registration.ReviewedByUserId = null;
        registration.ReviewedAt = DateTimeOffset.UtcNow;
        registration.Note = cleanNote;
        registration.ApplicantUser.IsActive = approve && registration.ApplicantUser.EmailConfirmed;
        var userUpdate = await userManager.UpdateAsync(registration.ApplicantUser);
        if (!userUpdate.Succeeded)
            throw new InvalidOperationException(string.Join(" ", userUpdate.Errors.Select(x => x.Description)));
        var stamp = await userManager.UpdateSecurityStampAsync(registration.ApplicantUser);
        if (!stamp.Succeeded)
            throw new InvalidOperationException(string.Join(" ", stamp.Errors.Select(x => x.Description)));

        if (!approve)
        {
            var now = DateTimeOffset.UtcNow;
            await db.DeviceSessions.Where(x => x.UserId == registration.ApplicantUserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now), cancellationToken);
            await db.PushRegistrations.Where(x => x.UserId == registration.ApplicantUserId && x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        }

        var title = approve ? "Başvurunuz onaylandı" : "Başvurunuz reddedildi";
        var body = approve
            ? registration.ApplicantUser.EmailConfirmed
                ? $"{registration.Company.Name} / {registration.Branch.Name} hesabınız aktif. Artık giriş yapabilirsiniz."
                : $"{registration.Company.Name} / {registration.Branch.Name} başvurunuz onaylandı. Giriş için e-posta adresinizi de doğrulayın."
            : $"{registration.Company.Name} / {registration.Branch.Name} başvurunuz onaylanmadı.{(cleanNote is null ? string.Empty : " Not: " + cleanNote)}";
        db.Notifications.Add(new ValeNotification
        {
            CompanyId = registration.CompanyId,
            BranchId = registration.BranchId,
            UserId = registration.ApplicantUserId,
            Title = title,
            Body = body,
            Type = approve ? "RegistrationApproved" : "RegistrationRejected"
        });
        await db.SaveChangesAsync(cancellationToken);
        var push = await pushSender.SendToUsersAsync(
            registration.CompanyId, [registration.ApplicantUserId], title, body,
            approve ? "RegistrationApproved" : "RegistrationRejected", registration.BranchId, cancellationToken);
        await audit.RecordAsync(
            approve ? "registration.approved" : "registration.rejected",
            "RegistrationRequest",
            registration.Id.ToString(),
            $"{registration.ApplicantUser.FullName} / {registration.Company.Name} / {registration.Branch.Name}. Push: {push.Delivered}/{push.Attempted}. Not: {cleanNote ?? "yok"}",
            cancellationToken);

        var message = approve && !registration.ApplicantUser.EmailConfirmed
            ? "Başvuru onaylandı; hesap e-posta doğrulamasından sonra açılacak."
            : approve ? "Başvuru onaylandı ve hesap açıldı." : "Başvuru reddedildi ve erişim kapalı tutuldu.";
        return RedirectWithMessage(nameof(Index), "Registrations", message);
    }
}
