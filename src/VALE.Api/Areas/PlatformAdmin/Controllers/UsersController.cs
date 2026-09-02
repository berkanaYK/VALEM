using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Areas.PlatformAdmin.Models;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

public sealed class UsersController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    PlatformAuditService audit,
    FirebasePushSender pushSender) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Where(x => x.CompanyId != null && x.Company != null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                x.Company!.Name.ToLower().Contains(term));
        }

        ViewBag.Search = search;
        var users = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Company!.Name)
            .ThenBy(x => x.FullName)
            .Take(250)
            .ToListAsync(cancellationToken);
        var rows = new List<PlatformUserRow>(users.Count);
        foreach (var user in users)
        {
            rows.Add(new PlatformUserRow(
                user.Id,
                user.CompanyId!.Value,
                user.Company!.Name,
                user.FullName,
                user.Email ?? string.Empty,
                user.Branch?.Name,
                user.EmailConfirmed,
                user.IsActive,
                user.LockoutEnd > DateTimeOffset.UtcNow,
                user.LastLoginAt,
                (await userManager.GetRolesAsync(user)).Where(x => x != Roles.PlatformAdmin).ToList()));
        }

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildEditModelAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PlatformUserEditModel model, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == model.Id && x.CompanyId != null, cancellationToken);
        if (user is null) return NotFound();

        var selectedRoles = model.SelectedRoles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedRoles.Count == 0 || selectedRoles.Any(x => !Roles.All.Contains(x, StringComparer.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(model.SelectedRoles), "En az bir geçerli firma rolü seçin.");

        var branch = await db.Branches.SingleOrDefaultAsync(
            x => x.Id == model.BranchId && x.CompanyId == user.CompanyId,
            cancellationToken);
        if (branch is null)
            ModelState.AddModelError(nameof(model.BranchId), "Seçilen şube bu firmaya ait değil.");

        var email = model.Email.Trim();
        var duplicateUser = await userManager.FindByEmailAsync(email);
        if (duplicateUser is not null && duplicateUser.Id != user.Id)
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta adresi başka bir hesapta kullanılıyor.");

        var isCanonicalOwner = user.Company?.OwnerUserId == user.Id;
        if (isCanonicalOwner && (!model.IsActive || !selectedRoles.Contains(Roles.Owner, StringComparer.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(model.SelectedRoles), "Firmanın ana sahibi aktif kalmalı ve Owner rolünü korumalıdır. Önce firma sahipliğini kontrollü biçimde devredin.");

        if (!ModelState.IsValid)
        {
            await PopulateEditMetadataAsync(model, user, cancellationToken);
            return View(model);
        }

        var previousRoles = await userManager.GetRolesAsync(user);
        var before = $"{user.FullName}, {user.Email}, aktif={user.IsActive}, roller={string.Join(',', previousRoles)}";
        user.FullName = model.FullName.Trim();
        user.Email = email;
        user.NormalizedEmail = userManager.NormalizeEmail(email);
        user.UserName = email;
        user.NormalizedUserName = userManager.NormalizeName(email);
        user.PhoneNumber = Clean(model.PhoneNumber);
        user.JobTitle = Clean(model.JobTitle);
        user.BranchId = branch!.Id;
        user.IsActive = model.IsActive;
        user.EmailConfirmed = model.EmailConfirmed;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            await PopulateEditMetadataAsync(model, user, cancellationToken);
            return View(model);
        }

        var removeRoles = previousRoles.Where(x => x != Roles.PlatformAdmin && !selectedRoles.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
        var addRoles = selectedRoles.Where(x => !previousRoles.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (removeRoles.Length > 0)
        {
            var result = await userManager.RemoveFromRolesAsync(user, removeRoles);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (addRoles.Length > 0)
        {
            var result = await userManager.AddToRolesAsync(user, addRoles);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        await EnsurePrimaryMembershipAsync(user, branch, cancellationToken);
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            throw new InvalidOperationException("Kullanıcının güvenlik damgası yenilenemedi.");

        var now = DateTimeOffset.UtcNow;
        await db.DeviceSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now), cancellationToken);
        if (!user.IsActive)
        {
            await db.PushRegistrations.Where(x => x.UserId == user.Id && x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        }

        await audit.RecordAsync(
            "user.updated",
            "AppUser",
            user.Id.ToString(),
            $"{before} → {user.FullName}, {user.Email}, aktif={user.IsActive}, roller={string.Join(',', selectedRoles)}. Cihaz oturumları kapatıldı.",
            cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Users", "Kullanıcı güncellendi ve eski cihaz oturumları kapatıldı.", new { id = user.Id });
    }

    [HttpPost]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindTenantUserAsync(id, cancellationToken);
        if (user is null) return NotFound();
        var revoked = await RevokeStoredAccessAsync(user, deactivatePush: true, cancellationToken);
        await audit.RecordAsync("user.sessions.revoked", "AppUser", user.Id.ToString(),
            $"{user.FullName} için tüm oturumlar ve {revoked.PushRegistrations} bildirim cihazı kapatıldı.", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Users", $"Tüm oturumlar kapatıldı ({revoked.DeviceSessions} hatırlanan cihaz, {revoked.PushRegistrations} bildirim cihazı).", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindTenantUserAsync(id, cancellationToken);
        if (user is null) return NotFound();
        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded) throw new InvalidOperationException(string.Join(" ", disabled.Errors.Select(x => x.Description)));
        var resetKey = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!resetKey.Succeeded) throw new InvalidOperationException(string.Join(" ", resetKey.Errors.Select(x => x.Description)));
        var revoked = await RevokeStoredAccessAsync(user, deactivatePush: true, cancellationToken);
        await audit.RecordAsync("user.twofactor.reset", "AppUser", user.Id.ToString(),
            $"{user.FullName} için Authenticator güvenliği destek talebiyle sıfırlandı; tüm oturumlar kapatıldı.", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Users", $"Authenticator sıfırlandı ve tüm oturumlar kapatıldı ({revoked.DeviceSessions} cihaz).", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SendNotification(Guid id, string? title, string? message, CancellationToken cancellationToken)
    {
        var user = await FindTenantUserAsync(id, cancellationToken);
        if (user is null) return NotFound();
        var cleanTitle = title?.Trim() ?? string.Empty;
        var cleanMessage = message?.Trim() ?? string.Empty;
        if (cleanTitle.Length is < 3 or > 140 || cleanMessage.Length is < 3 or > 600)
        {
            TempData["Error"] = "Bildirim başlığı 3-140, mesajı 3-600 karakter arasında olmalıdır.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        db.Notifications.Add(new ValeNotification
        {
            CompanyId = user.CompanyId!.Value,
            BranchId = user.BranchId,
            UserId = user.Id,
            Title = cleanTitle,
            Body = cleanMessage,
            Type = "PlatformSupport"
        });
        await db.SaveChangesAsync(cancellationToken);
        var push = await pushSender.SendToUsersAsync(
            user.CompanyId.Value, [user.Id], cleanTitle, cleanMessage, "PlatformSupport", user.BranchId, cancellationToken);
        await audit.RecordAsync("user.support.notification", "AppUser", user.Id.ToString(),
            $"{user.FullName} kullanıcısına '{cleanTitle}' başlıklı destek bildirimi gönderildi. Push: {push.Delivered}/{push.Attempted}.", cancellationToken);
        return RedirectWithMessage(nameof(Edit), "Users", $"Destek bildirimi kaydedildi; push teslimi {push.Delivered}/{push.Attempted}.", new { id });
    }

    private async Task<PlatformUserEditModel?> BuildEditModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.AsNoTracking()
            .Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == id && x.CompanyId != null, cancellationToken);
        if (user is null) return null;

        var model = new PlatformUserEditModel
        {
            Id = user.Id,
            CompanyId = user.CompanyId!.Value,
            CompanyName = user.Company?.Name ?? "Firma",
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            JobTitle = user.JobTitle,
            BranchId = user.BranchId ?? Guid.Empty,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            SelectedRoles = (await userManager.GetRolesAsync(user)).Where(x => x != Roles.PlatformAdmin).ToList(),
            IsLocked = user.LockoutEnd > DateTimeOffset.UtcNow,
            LastLoginAt = user.LastLoginAt
        };
        await PopulateEditMetadataAsync(model, user, cancellationToken);
        return model;
    }

    private async Task PopulateEditMetadataAsync(PlatformUserEditModel model, AppUser user, CancellationToken cancellationToken)
    {
        var companyId = user.CompanyId!.Value;
        model.CompanyId = companyId;
        model.CompanyName = user.Company?.Name ?? await db.Companies.AsNoTracking()
            .Where(x => x.Id == companyId).Select(x => x.Name).SingleAsync(cancellationToken);
        var branches = await db.Branches.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
            .Select(x => new { x.Id, Display = x.Name + (x.IsActive ? "" : " (pasif)") })
            .ToListAsync(cancellationToken);
        model.BranchIds = branches.Select(x => x.Id).ToList();
        model.BranchNames = branches.Select(x => x.Display).ToList();
        model.AvailableRoles = Roles.All.ToList();
        model.IsLocked = user.LockoutEnd > DateTimeOffset.UtcNow;
        model.LastLoginAt = user.LastLoginAt;
        model.ActiveDeviceSessions = await db.DeviceSessions.AsNoTracking().CountAsync(
            x => x.UserId == user.Id && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        model.ActivePushRegistrations = await db.PushRegistrations.AsNoTracking().CountAsync(
            x => x.UserId == user.Id && x.IsActive,
            cancellationToken);
    }

    private Task<AppUser?> FindTenantUserAsync(Guid id, CancellationToken cancellationToken) =>
        userManager.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId != null, cancellationToken);

    private async Task<(int DeviceSessions, int PushRegistrations)> RevokeStoredAccessAsync(
        AppUser user,
        bool deactivatePush,
        CancellationToken cancellationToken)
    {
        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) throw new InvalidOperationException(string.Join(" ", stamp.Errors.Select(x => x.Description)));
        var now = DateTimeOffset.UtcNow;
        var sessions = await db.DeviceSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now), cancellationToken);
        var pushes = deactivatePush
            ? await db.PushRegistrations.Where(x => x.UserId == user.Id && x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken)
            : 0;
        return (sessions, pushes);
    }

    private async Task EnsurePrimaryMembershipAsync(AppUser user, Branch branch, CancellationToken cancellationToken)
    {
        var memberships = await db.UserBranchMemberships
            .Where(x => x.CompanyId == user.CompanyId && x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var previousPrimaries = memberships.Where(x => x.IsPrimary && x.BranchId != branch.Id).ToList();
        foreach (var membership in previousPrimaries)
        {
            membership.IsPrimary = false;
            membership.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (previousPrimaries.Count > 0) await db.SaveChangesAsync(cancellationToken);

        var primary = memberships.SingleOrDefault(x => x.BranchId == branch.Id);
        if (primary is null)
        {
            db.UserBranchMemberships.Add(new UserBranchMembership
            {
                CompanyId = user.CompanyId!.Value,
                UserId = user.Id,
                BranchId = branch.Id,
                IsPrimary = true,
                IsActive = true
            });
        }
        else
        {
            primary.IsPrimary = true;
            primary.IsActive = true;
            primary.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
