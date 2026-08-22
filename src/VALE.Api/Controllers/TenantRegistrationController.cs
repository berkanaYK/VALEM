using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/registration")]
public sealed class TenantRegistrationController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    CurrentUserContext currentUser,
    TenantAccessService tenantAccess,
    AuditService audit,
    FirebasePushSender pushSender,
    IValeEmailSender emailSender,
    IOptions<EmailOptions> emailOptions) : ControllerBase
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    [HttpPost("owner")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<RegisterResponse>> RegisterOwner(OwnerRegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var loginMethod = NormalizeLoginMethod(request.LoginMethod);
        ValidatePasswordForMethod(loginMethod, request.Password);
        var companyCode = Normalize(request.CompanyCode);
        var branchCode = Normalize(request.FirstBranchCode);
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresiyle bir hesap zaten bulunuyor.");
        if (await db.Companies.AnyAsync(x => x.Code == companyCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Firma kodu kullanımda", "Farklı ve size özel bir firma kodu seçin.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var company = new Company
        {
            Name = request.CompanyName.Trim(),
            Code = companyCode,
            IsActive = true
        };
        db.Companies.Add(company);
        var branch = new Branch
        {
            Company = company,
            CompanyId = company.Id,
            Name = request.FirstBranchName.Trim(),
            Code = branchCode,
            City = Clean(request.City) ?? string.Empty,
            Address = string.Empty,
            InviteCode = GenerateInviteCode(companyCode, branchCode),
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.PhoneNumber),
            CompanyId = company.Id,
            Company = company,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = false,
            JobTitle = "Firma Sahibi"
        };
        var create = await CreateUserAsync(user, request.Password, loginMethod);
        if (!create.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest, "Hesap oluşturulamadı", string.Join(" ", create.Errors.Select(x => x.Description)));
        var role = await userManager.AddToRoleAsync(user, Roles.Owner);
        if (!role.Succeeded)
            throw new ApiException(StatusCodes.Status500InternalServerError, "Yetki atanamadı", string.Join(" ", role.Errors.Select(x => x.Description)));

        company.OwnerUserId = user.Id;
        db.UserBranchMemberships.Add(new UserBranchMembership
        {
            CompanyId = company.Id,
            Company = company,
            UserId = user.Id,
            User = user,
            BranchId = branch.Id,
            Branch = branch,
            IsPrimary = true,
            IsActive = true
        });
        db.Notifications.Add(new ValeNotification
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            Title = "VALE firmanız hazır",
            Body = $"{company.Name} ve {branch.Name} şubesi oluşturuldu. E-posta doğrulamasından sonra hesabınız girişe açılacak.",
            Type = "CompanyCreated"
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(user.Id, branch.Id, "company.created", "Company", company.Id.ToString(), $"{company.Name} ({company.Code}) oluşturuldu. Giriş yöntemi: {loginMethod}.", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var sent = await TrySendConfirmationAsync(user, cancellationToken);
        var methodHint = LoginMethodHint(loginMethod);
        var message = sent
            ? $"Firma ve yönetici hesabınız oluşturuldu. {email} adresine 'Bu e-posta sizin mi?' doğrulama bağlantısı gönderdik. Linki onayladıktan sonra {methodHint}"
            : $"Firma ve hesabınız oluşturuldu ancak doğrulama e-postası gönderilemedi. Daha sonra doğrulama e-postasını yeniden isteyin. {methodHint}";
        return Created("/api/registration/owner", new RegisterResponse(message, false));
    }

    [HttpPost("staff")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<RegisterResponse>> RegisterStaff(StaffRegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var loginMethod = NormalizeLoginMethod(request.LoginMethod);
        ValidatePasswordForMethod(loginMethod, request.Password);
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresiyle bir hesap zaten bulunuyor.");

        Branch? branch;
        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            var invite = Normalize(request.InviteCode);
            branch = await db.Branches.Include(x => x.Company)
                .SingleOrDefaultAsync(x => x.IsActive && x.Company.IsActive && x.InviteCode == invite, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.CompanyCode) || string.IsNullOrWhiteSpace(request.BranchCode))
                throw new ApiException(StatusCodes.Status400BadRequest, "Firma ve şube gerekli", "Davet kodu kullanmıyorsanız firma kodu ile şube kodunu birlikte girin.");
            var companyCode = Normalize(request.CompanyCode);
            var branchCode = Normalize(request.BranchCode);
            branch = await db.Branches.Include(x => x.Company)
                .SingleOrDefaultAsync(x => x.IsActive && x.Company.IsActive && x.Company.Code == companyCode && x.Code == branchCode, cancellationToken);
        }
        if (branch is null)
            throw new ApiException(StatusCodes.Status404NotFound, "Firma / şube bulunamadı", "Davet kodunu veya firma ve şube kodlarını kontrol edin.");

        var employeeCode = Clean(request.EmployeeCode)?.ToUpperInvariant();
        if (employeeCode is not null && await userManager.Users.AnyAsync(x => x.CompanyId == branch.CompanyId && x.EmployeeCode == employeeCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Personel kodu kullanımda", "Bu personel kodu başka bir hesapta kayıtlı.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.PhoneNumber),
            EmployeeCode = employeeCode,
            CompanyId = branch.CompanyId,
            Company = branch.Company,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = false
        };
        var create = await CreateUserAsync(user, request.Password, loginMethod);
        if (!create.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest, "Hesap oluşturulamadı", string.Join(" ", create.Errors.Select(x => x.Description)));
        var role = await userManager.AddToRoleAsync(user, Roles.Valet);
        if (!role.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ApiException(StatusCodes.Status500InternalServerError, "Başlangıç rolü atanamadı", string.Join(" ", role.Errors.Select(x => x.Description)));
        }

        var registration = new RegistrationRequest
        {
            CompanyId = branch.CompanyId,
            Company = branch.Company,
            BranchId = branch.Id,
            Branch = branch,
            ApplicantUserId = user.Id,
            ApplicantUser = user,
            RequestedRole = Roles.Valet,
            Status = "Pending"
        };
        db.RegistrationRequests.Add(registration);
        db.UserBranchMemberships.Add(new UserBranchMembership
        {
            CompanyId = branch.CompanyId,
            Company = branch.Company,
            UserId = user.Id,
            User = user,
            BranchId = branch.Id,
            Branch = branch,
            IsPrimary = true,
            IsActive = true
        });
        var managerIds = await AddApprovalNotificationsAsync(user, branch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (managerIds.Count > 0)
        {
            await pushSender.SendToUsersAsync(
                branch.CompanyId,
                managerIds,
                "Yeni personel başvurusu",
                $"{user.FullName}, {branch.Name} şubesine katılmak için onay bekliyor.",
                "RegistrationPending",
                branch.Id,
                cancellationToken);
        }

        await audit.RecordAsync(user.Id, branch.Id, "account.registration.requested", "RegistrationRequest", registration.Id.ToString(), $"Personel katılım başvurusu oluşturuldu. Giriş yöntemi: {loginMethod}.", cancellationToken: cancellationToken);
        var sent = await TrySendConfirmationAsync(user, cancellationToken);
        var verificationText = sent
            ? $"{email} adresine e-posta sahipliği doğrulama bağlantısı gönderdik."
            : "Doğrulama e-postası şu anda gönderilemedi; daha sonra yeniden isteyin.";
        return Created("/api/registration/staff", new RegisterResponse($"Başvurunuz {branch.Company.Name} / {branch.Name} yöneticilerine gönderildi. {verificationText} Giriş için hem e-posta doğrulaması hem yönetici onayı tamamlanmalıdır. {LoginMethodHint(loginMethod)}", true));
    }

    [HttpGet("pending")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<IReadOnlyList<RegistrationRequestDto>>> GetPending(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId;
        var query = db.RegistrationRequests.AsNoTracking()
            .Include(x => x.ApplicantUser).Include(x => x.Company).Include(x => x.Branch)
            .Where(x => x.CompanyId == companyId && x.ApplicantUser.CompanyId == companyId && x.Branch.CompanyId == companyId && x.Status == "Pending");
        if (!currentUser.CanAccessAllBranches)
        {
            var branchId = await tenantAccess.ResolveBranchIdAsync(null, cancellationToken);
            query = query.Where(x => x.BranchId == branchId);
        }
        var rows = await query.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost("{requestId:guid}/decision")]
    [Authorize(Policy = Roles.ManageUsersPolicy)]
    public async Task<ActionResult<RegistrationRequestDto>> Decide(Guid requestId, RegistrationDecisionRequest request, CancellationToken cancellationToken)
    {
        var registration = await db.RegistrationRequests
            .Include(x => x.ApplicantUser).Include(x => x.Company).Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CompanyId == currentUser.CompanyId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Başvuru bulunamadı", "Bu firmaya ait başvuru bulunamadı.");
        if (registration.Status != "Pending")
            throw new ApiException(StatusCodes.Status409Conflict, "Başvuru sonuçlanmış", "Bu başvuru daha önce incelenmiş.");
        if (registration.Branch.CompanyId != currentUser.CompanyId ||
            registration.ApplicantUser.CompanyId != currentUser.CompanyId ||
            registration.ApplicantUser.BranchId != registration.BranchId)
            throw new ApiException(StatusCodes.Status404NotFound, "Başvuru bulunamadı", "Başvuru firma veya şube kapsamıyla eşleşmiyor.");
        await tenantAccess.EnsureBranchAccessAsync(registration.BranchId, cancellationToken);

        registration.Status = request.Approve ? "Approved" : "Rejected";
        registration.ReviewedByUserId = currentUser.UserId;
        registration.ReviewedAt = DateTimeOffset.UtcNow;
        registration.Note = Clean(request.Note);
        registration.ApplicantUser.IsActive = request.Approve && registration.ApplicantUser.EmailConfirmed;
        var update = await userManager.UpdateAsync(registration.ApplicantUser);
        if (!update.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest, "Hesap güncellenemedi", string.Join(" ", update.Errors.Select(x => x.Description)));
        await userManager.UpdateSecurityStampAsync(registration.ApplicantUser);

        db.Notifications.Add(new ValeNotification
        {
            CompanyId = registration.CompanyId,
            BranchId = registration.BranchId,
            UserId = registration.ApplicantUserId,
            Title = request.Approve ? "Başvurunuz onaylandı" : "Başvurunuz reddedildi",
            Body = request.Approve
                ? registration.ApplicantUser.EmailConfirmed
                    ? $"{registration.Company.Name} / {registration.Branch.Name} hesabınız aktif. Artık giriş yapabilirsiniz."
                    : $"{registration.Company.Name} / {registration.Branch.Name} başvurunuz onaylandı. Hesabın girişe açılması için e-posta doğrulama bağlantısını da onaylayın."
                : $"{registration.Company.Name} / {registration.Branch.Name} başvurunuz onaylanmadı.{(string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " Not: " + request.Note.Trim())}",
            Type = request.Approve ? "RegistrationApproved" : "RegistrationRejected"
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, registration.BranchId, request.Approve ? "account.registration.approved" : "account.registration.rejected", "RegistrationRequest", registration.Id.ToString(), request.Note ?? string.Empty, cancellationToken: cancellationToken);
        return Ok(Map(registration));
    }

    private async Task<IdentityResult> CreateUserAsync(AppUser user, string? password, string loginMethod)
    {
        if (loginMethod == LoginMethods.EmailCode)
            return await userManager.CreateAsync(user);
        return await userManager.CreateAsync(user, password!);
    }

    private async Task<bool> TrySendConfirmationAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured || string.IsNullOrWhiteSpace(user.Email)) return false;
        try
        {
            var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
            var url = BuildConfirmationUrl(user.Id, encodedToken);
            await emailSender.SendEmailConfirmationLinkAsync(user.Email, user.FullName, url, cancellationToken);
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    private string BuildConfirmationUrl(Guid userId, string encodedToken)
    {
        if (!Uri.TryCreate(_emailOptions.PublicBaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta doğrulama hazır değil", "Güvenilir doğrulama adresi yapılandırılmamış.");
        var root = baseUri.ToString().TrimEnd('/');
        return $"{root}/api/auth/confirm-email?userId={userId:D}&token={Uri.EscapeDataString(encodedToken)}";
    }

    private async Task<IReadOnlyList<Guid>> AddApprovalNotificationsAsync(AppUser applicant, Branch branch, CancellationToken cancellationToken)
    {
        var candidates = await userManager.Users.AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == branch.CompanyId)
            .ToListAsync(cancellationToken);
        var managerIds = new List<Guid>();
        foreach (var manager in candidates)
        {
            var roles = await userManager.GetRolesAsync(manager);
            if (!roles.Any(r => Roles.ManageUsersRoles.Contains(r, StringComparer.OrdinalIgnoreCase))) continue;
            if (roles.Contains(Roles.BranchManager, StringComparer.OrdinalIgnoreCase) &&
                manager.BranchId != branch.Id &&
                !await db.UserBranchMemberships.AsNoTracking().AnyAsync(x =>
                    x.CompanyId == branch.CompanyId && x.UserId == manager.Id && x.BranchId == branch.Id && x.IsActive,
                    cancellationToken)) continue;
            managerIds.Add(manager.Id);
            db.Notifications.Add(new ValeNotification
            {
                CompanyId = branch.CompanyId,
                BranchId = branch.Id,
                UserId = manager.Id,
                Title = "Yeni personel başvurusu",
                Body = $"{applicant.FullName}, {branch.Name} şubesine katılmak için onay bekliyor.",
                Type = "RegistrationPending"
            });
        }
        return managerIds;
    }

    private static string NormalizeLoginMethod(string? value)
    {
        var method = LoginMethods.All.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return method ?? throw new ApiException(StatusCodes.Status400BadRequest, "Giriş yöntemi geçersiz", "Parola, e-posta kodu veya Authenticator seçeneklerinden birini kullanın.");
    }

    private static void ValidatePasswordForMethod(string loginMethod, string? password)
    {
        if (loginMethod == LoginMethods.EmailCode) return;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            throw new ApiException(StatusCodes.Status400BadRequest, "Parola gerekli", "Parola veya Authenticator girişini seçtiyseniz en az 10 karakterlik güçlü bir parola oluşturun.");
    }

    private static string LoginMethodHint(string loginMethod) => loginMethod switch
    {
        LoginMethods.EmailCode => "Giriş ekranında 'E-posta Koduyla Giriş' seçeneğini kullanabilirsiniz.",
        LoginMethods.Authenticator => "İlk girişinizi parolanızla yaptıktan sonra Authenticator Güvenliği ekranından QR kurulumunu tamamlayın; sonraki girişlerde Authenticator seçeneğini kullanın.",
        _ => "E-posta + parolanızla giriş yapabilirsiniz."
    };

    private static RegistrationRequestDto Map(RegistrationRequest x) => new(
        x.Id, x.ApplicantUserId, x.ApplicantUser.FullName, x.ApplicantUser.Email ?? string.Empty,
        x.CompanyId, x.Company.Name, x.BranchId, x.Branch.Name, x.RequestedRole, x.Status,
        x.CreatedAt, x.ReviewedAt, x.Note);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string GenerateInviteCode(string companyCode, string branchCode) => $"{companyCode}-{branchCode}-{Guid.NewGuid():N}"[..Math.Min(40, companyCode.Length + branchCode.Length + 10)].ToUpperInvariant();
}
