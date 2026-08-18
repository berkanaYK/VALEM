using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/registration")]
public sealed class RegistrationController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    CurrentUserContext currentUser,
    NotificationService notifications,
    AuditService audit) : ControllerBase
{
    [HttpGet("company/{companyCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationCompanyDto>> GetCompany(string companyCode, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(companyCode, 30);
        var company = await db.Companies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Firma bulunamadı", "Firma kodunu kontrol edin.");
        var branches = await db.Branches.AsNoTracking()
            .Where(x => x.CompanyId == company.Id && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new RegistrationBranchDto(x.Id, x.Code, x.Name, x.City))
            .ToListAsync(cancellationToken);
        return Ok(new RegistrationCompanyDto(company.Id, company.Code, company.Name, branches));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresiyle bir hesap zaten bulunuyor.");

        return request.Kind == RegistrationKind.CompanyOwner
            ? await RegisterCompanyOwnerAsync(request, email, cancellationToken)
            : await RegisterStaffAsync(request, email, cancellationToken);
    }

    [HttpGet("approvals")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<IReadOnlyList<RegistrationRequestDto>>> GetApprovals(
        [FromQuery] RegistrationApprovalStatus? status = RegistrationApprovalStatus.Pending,
        CancellationToken cancellationToken = default)
    {
        var query = db.RegistrationRequests.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Where(x => x.CompanyId == currentUser.CompanyId);

        if (!currentUser.CanAccessAllBranches)
        {
            var branchId = currentUser.ResolveBranchId(null);
            query = query.Where(x => x.BranchId == branchId);
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapRequest).ToList());
    }

    [HttpPost("approvals/{requestId:guid}/approve")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<RegistrationRequestDto>> Approve(Guid requestId, ReviewRegistrationRequest review, CancellationToken cancellationToken)
    {
        var request = await GetManageableRequestAsync(requestId, cancellationToken);
        if (request.Status != RegistrationApprovalStatus.Pending)
            throw new ApiException(StatusCodes.Status409Conflict, "Başvuru kapalı", "Bu başvuru daha önce sonuçlandırılmış.");

        var role = string.IsNullOrWhiteSpace(review.Role) ? request.RequestedRole : review.Role.Trim();
        if (!Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase) || role.Equals(Roles.Owner, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol geçersiz", "Personel başvurusuna geçerli bir firma rolü seçin.");
        if (!Roles.CanAssignRoles(currentUser.RoleNames, [role]))
            throw new ApiException(StatusCodes.Status403Forbidden, "Rol atanamaz", "Bu rolü atamak için yeterli yetkiniz yok.");

        var existingRoles = await userManager.GetRolesAsync(request.User);
        if (existingRoles.Count > 0)
        {
            var remove = await userManager.RemoveFromRolesAsync(request.User, existingRoles);
            if (!remove.Succeeded) throw IdentityFailure("Eski roller kaldırılamadı", remove);
        }
        var add = await userManager.AddToRoleAsync(request.User, role);
        if (!add.Succeeded) throw IdentityFailure("Rol atanamadı", add);

        request.User.IsActive = true;
        request.User.JobTitle ??= RoleTitle(role);
        var update = await userManager.UpdateAsync(request.User);
        if (!update.Succeeded) throw IdentityFailure("Kullanıcı etkinleştirilemedi", update);
        await userManager.UpdateSecurityStampAsync(request.User);

        request.Status = RegistrationApprovalStatus.Approved;
        request.RequestedRole = role;
        request.ReviewedByUserId = currentUser.UserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewNote = Clean(review.Note);
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateForUserAsync(request.CompanyId, request.UserId, request.BranchId,
            "Hesabınız onaylandı", $"{request.Branch.Name} şubesindeki VALE hesabınız {RoleTitle(role)} rolüyle açıldı.",
            "approval-approved", "RegistrationRequest", request.Id.ToString(), cancellationToken);
        await audit.RecordAsync(currentUser.UserId, request.BranchId, "registration.approved", "RegistrationRequest", request.Id.ToString(),
            $"{request.User.FullName} başvurusu onaylandı. Rol: {role}", cancellationToken: cancellationToken);
        return Ok(MapRequest(request));
    }

    [HttpPost("approvals/{requestId:guid}/reject")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<RegistrationRequestDto>> Reject(Guid requestId, ReviewRegistrationRequest review, CancellationToken cancellationToken)
    {
        var request = await GetManageableRequestAsync(requestId, cancellationToken);
        if (request.Status != RegistrationApprovalStatus.Pending)
            throw new ApiException(StatusCodes.Status409Conflict, "Başvuru kapalı", "Bu başvuru daha önce sonuçlandırılmış.");

        request.Status = RegistrationApprovalStatus.Rejected;
        request.ReviewedByUserId = currentUser.UserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewNote = Clean(review.Note);
        request.User.IsActive = false;
        await userManager.UpdateAsync(request.User);
        await userManager.UpdateSecurityStampAsync(request.User);
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateForUserAsync(request.CompanyId, request.UserId, request.BranchId,
            "Başvurunuz sonuçlandı", string.IsNullOrWhiteSpace(request.ReviewNote) ? "VALE personel başvurunuz onaylanmadı." : $"Başvuru notu: {request.ReviewNote}",
            "approval-rejected", "RegistrationRequest", request.Id.ToString(), cancellationToken);
        await audit.RecordAsync(currentUser.UserId, request.BranchId, "registration.rejected", "RegistrationRequest", request.Id.ToString(),
            $"{request.User.FullName} başvurusu reddedildi.", cancellationToken: cancellationToken);
        return Ok(MapRequest(request));
    }

    [HttpPost("invites")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<CompanyInviteDto>> CreateInvite(CreateCompanyInviteRequest request, CancellationToken cancellationToken)
    {
        await currentUser.EnsureBranchAccessAsync(request.BranchId, cancellationToken);
        var branch = await db.Branches.Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == request.BranchId && x.CompanyId == currentUser.CompanyId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Davet için aktif bir şube seçin.");
        var role = request.Role.Trim();
        if (role.Equals(Roles.Owner, StringComparison.OrdinalIgnoreCase) || !Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol geçersiz", "Davet için geçerli bir personel rolü seçin.");
        if (!Roles.CanAssignRoles(currentUser.RoleNames, [role]))
            throw new ApiException(StatusCodes.Status403Forbidden, "Rol atanamaz", "Bu rol için davet oluşturma yetkiniz yok.");

        string code;
        do { code = $"VALE-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}"; }
        while (await db.CompanyInvites.AnyAsync(x => x.Code == code, cancellationToken));

        var invite = new CompanyInvite
        {
            CompanyId = currentUser.CompanyId,
            Company = branch.Company,
            BranchId = branch.Id,
            Branch = branch,
            Code = code,
            Role = role,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(request.ValidDays),
            MaxUses = request.MaxUses,
            CreatedByUserId = currentUser.UserId
        };
        db.CompanyInvites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, branch.Id, "invite.created", "CompanyInvite", invite.Id.ToString(),
            $"{branch.Name} için {role} daveti oluşturuldu.", cancellationToken: cancellationToken);
        return Created($"/api/registration/invites/{invite.Id}", MapInvite(invite));
    }

    [HttpGet("invites")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<IReadOnlyList<CompanyInviteDto>>> GetInvites(CancellationToken cancellationToken)
    {
        var query = db.CompanyInvites.AsNoTracking().Include(x => x.Company).Include(x => x.Branch)
            .Where(x => x.CompanyId == currentUser.CompanyId);
        if (!currentUser.CanAccessAllBranches)
        {
            var branchId = currentUser.ResolveBranchId(null);
            query = query.Where(x => x.BranchId == branchId);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapInvite).ToList());
    }

    private async Task<ActionResult<RegisterResponse>> RegisterCompanyOwnerAsync(RegisterRequest request, string email, CancellationToken cancellationToken)
    {
        var companyName = Required(request.CompanyName, "Firma adı", 160);
        var companyCode = NormalizeCode(Required(request.CompanyCode, "Firma kodu", 30), 30);
        var branchName = Required(request.BranchName, "İlk şube adı", 120);
        var branchCode = NormalizeCode(Required(request.BranchCode, "Şube kodu", 20), 20);
        if (await db.Companies.AnyAsync(x => x.Code == companyCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Firma kodu kullanımda", "Başka bir firma kodu seçin.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var company = new Company { Code = companyCode, Name = companyName };
        var branch = new Branch
        {
            Company = company,
            Code = branchCode,
            Name = branchName,
            City = Clean(request.BranchCity) ?? "Belirtilmedi",
            Address = Clean(request.BranchAddress) ?? string.Empty
        };
        db.Companies.Add(company);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.PhoneNumber),
            EmployeeCode = Clean(request.EmployeeCode)?.ToUpperInvariant(),
            JobTitle = "Firma Sahibi",
            CompanyId = company.Id,
            Company = company,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = true
        };
        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded) throw IdentityFailure("Firma sahibi hesabı oluşturulamadı", create);
        var roleResult = await userManager.AddToRolesAsync(user, [Roles.Owner, Roles.Admin]);
        if (!roleResult.Succeeded) throw IdentityFailure("Firma sahibi yetkileri atanamadı", roleResult);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await audit.RecordAsync(user.Id, branch.Id, "company.registered", "Company", company.Id.ToString(),
            $"{company.Name} firması ve {branch.Name} şubesi oluşturuldu.", cancellationToken: cancellationToken);
        return Created("/api/registration/register", new RegisterResponse("Firma hesabınız hazır. Yönetici hesabınızla hemen giriş yapabilirsiniz.", false));
    }

    private async Task<ActionResult<RegisterResponse>> RegisterStaffAsync(RegisterRequest request, string email, CancellationToken cancellationToken)
    {
        Company company;
        Branch branch;
        string requestedRole;
        CompanyInvite? invite = null;

        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            var inviteCode = NormalizeCode(request.InviteCode, 40);
            invite = await db.CompanyInvites.Include(x => x.Company).Include(x => x.Branch)
                .SingleOrDefaultAsync(x => x.Code == inviteCode, cancellationToken)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Davet bulunamadı", "Davet kodunu kontrol edin.");
            if (!invite.IsActive || invite.ExpiresAt <= DateTimeOffset.UtcNow || invite.UsedCount >= invite.MaxUses || !invite.Company.IsActive || !invite.Branch.IsActive)
                throw new ApiException(StatusCodes.Status410Gone, "Davet geçersiz", "Davet kodunun süresi dolmuş veya kullanım hakkı bitmiş.");
            company = invite.Company;
            branch = invite.Branch;
            requestedRole = invite.Role;
        }
        else
        {
            var companyCode = NormalizeCode(Required(request.CompanyCode, "Firma kodu", 30), 30);
            var branchCode = NormalizeCode(Required(request.BranchCode, "Şube kodu", 20), 20);
            company = await db.Companies.SingleOrDefaultAsync(x => x.Code == companyCode && x.IsActive, cancellationToken)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Firma bulunamadı", "Firma kodunu kontrol edin.");
            branch = await db.Branches.SingleOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == branchCode && x.IsActive, cancellationToken)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Bu firmada girilen şube kodu bulunamadı.");
            requestedRole = Roles.Valet;
        }

        var employeeCode = Clean(request.EmployeeCode)?.ToUpperInvariant();
        if (employeeCode is not null && await userManager.Users.AnyAsync(x => x.CompanyId == company.Id && x.EmployeeCode == employeeCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Personel kodu kullanımda", "Bu personel kodu aynı firmada başka bir kullanıcıya ait.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.PhoneNumber),
            EmployeeCode = employeeCode,
            CompanyId = company.Id,
            Company = company,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = false
        };
        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded) throw IdentityFailure("Hesap oluşturulamadı", create);

        var registration = new RegistrationRequest
        {
            CompanyId = company.Id,
            Company = company,
            BranchId = branch.Id,
            Branch = branch,
            UserId = user.Id,
            User = user,
            RequestedRole = requestedRole,
            Status = RegistrationApprovalStatus.Pending
        };
        db.RegistrationRequests.Add(registration);
        if (invite is not null)
        {
            invite.UsedCount++;
            if (invite.UsedCount >= invite.MaxUses) invite.IsActive = false;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await notifications.NotifyApproversAsync(company.Id, branch.Id, "Yeni personel başvurusu",
            $"{user.FullName}, {branch.Name} şubesine katılmak için onay bekliyor.", "RegistrationRequest", registration.Id.ToString(), cancellationToken);
        await audit.RecordAsync(user.Id, branch.Id, "account.registration.requested", "RegistrationRequest", registration.Id.ToString(),
            $"{company.Name} / {branch.Name} için personel başvurusu oluşturuldu.", cancellationToken: cancellationToken);
        return Created("/api/registration/register", new RegisterResponse("Başvurunuz alındı. Firmanızdaki yetkili onayladığında giriş yapabilirsiniz.", true));
    }

    private async Task<RegistrationRequest> GetManageableRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.RegistrationRequests.Include(x => x.User).Include(x => x.Company).Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CompanyId == currentUser.CompanyId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Başvuru bulunamadı", "Başvuru hesabınızın firmasına ait değil veya bulunamadı.");
        if (!currentUser.CanAccessAllBranches && request.BranchId != currentUser.ResolveBranchId(null))
            throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu şubenin başvurularını yönetemezsiniz.");
        return request;
    }

    private static RegistrationRequestDto MapRequest(RegistrationRequest x) => new(
        x.Id, x.UserId, x.User.FullName, x.User.Email ?? string.Empty, x.CompanyId, x.Company.Name,
        x.BranchId, x.Branch.Name, x.RequestedRole, x.Status, x.CreatedAt, x.ReviewedAt, x.ReviewNote);

    private static CompanyInviteDto MapInvite(CompanyInvite x) => new(
        x.Id, x.Code, x.CompanyId, x.Company.Name, x.BranchId, x.Branch.Name, x.Role, x.IsActive,
        x.ExpiresAt, x.MaxUses, x.UsedCount, x.CreatedAt);

    private static ApiException IdentityFailure(string title, IdentityResult result) =>
        new(StatusCodes.Status400BadRequest, title, string.Join(" ", result.Errors.Select(x => x.Description)));

    private static string Required(string? value, string field, int max)
    {
        var clean = Clean(value);
        if (string.IsNullOrWhiteSpace(clean)) throw new ApiException(StatusCodes.Status400BadRequest, $"{field} gerekli", $"{field} alanını doldurun.");
        return clean.Length <= max ? clean : clean[..max];
    }

    private static string NormalizeCode(string value, int max) =>
        new(value.Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Take(max).ToArray());

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RoleTitle(string role) => role switch
    {
        Roles.Admin => "Yönetici",
        Roles.OperationsManager or Roles.Manager => "Operasyon Yöneticisi",
        Roles.BranchManager => "Şube Müdürü",
        Roles.Supervisor => "Süpervizör",
        Roles.Cashier => "Kasiyer",
        Roles.Auditor => "Denetçi",
        _ => "Vale Personeli"
    };
}
