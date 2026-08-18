using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Authorize(Policy = Roles.ManageUsersPolicy)]
[Route("api/admin")]
public sealed class AdminController(ValeDbContext db, UserManager<AppUser> userManager, CurrentUserContext currentUser, AuditService audit) : ControllerBase
{
    [HttpPost("branches")]
    [Authorize(Policy = Roles.ManageBranchesPolicy)]
    public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(x => x.CompanyId == currentUser.CompanyId && x.Code == code, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Şube kodu kullanımda", "Bu şube kodu firmanızda daha önce kullanılmış.");
        var branch = new Branch
        {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            City = request.City.Trim(),
            Address = request.Address?.Trim() ?? string.Empty,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, branch.Id, "branch.created", "Branch", branch.Id.ToString(), $"{branch.Code} - {branch.Name} oluşturuldu.", cancellationToken: cancellationToken);
        return Created($"/api/branches/{branch.Id}", new BranchDto(branch.Id, branch.Code, branch.Name, branch.City, branch.Address, branch.IsActive));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsNoTracking().Include(x => x.Branch)
            .Where(x => x.CompanyId == currentUser.CompanyId);
        if (!currentUser.CanAccessAllBranches)
        {
            var branchId = currentUser.ResolveBranchId(null);
            query = query.Where(x => x.BranchId == branchId);
        }
        var users = await query.OrderBy(x => x.IsActive).ThenBy(x => x.FullName).ToListAsync(cancellationToken);
        var result = new List<AdminUserDto>(users.Count);
        foreach (var user in users) result.Add(MapUser(user, await userManager.GetRolesAsync(user)));
        return Ok(result);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(userId, cancellationToken, allowSelf: true);
        return Ok(await MapDetailAsync(user));
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await currentUser.EnsureBranchAccessAsync(request.BranchId, cancellationToken);
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == request.BranchId && x.CompanyId == currentUser.CompanyId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Kullanıcı için seçilen şube aktif değil.");
        var selectedRoles = NormalizeRoles(request.Roles);
        EnsureCanAssign(selectedRoles);
        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresine ait kullanıcı zaten var.");
        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            CompanyId = currentUser.CompanyId,
            BranchId = branch.Id,
            Branch = branch,
            IsActive = true
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Kullanıcı oluşturulamadı", string.Join(" ", createResult.Errors.Select(x => x.Description)));
        var roleResult = await userManager.AddToRolesAsync(user, selectedRoles);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol atanamadı", string.Join(" ", roleResult.Errors.Select(x => x.Description)));
        }
        await audit.RecordAsync(currentUser.UserId, branch.Id, "user.created", "User", user.Id.ToString(), $"{user.FullName} oluşturuldu. Roller: {string.Join(", ", selectedRoles)}", cancellationToken: cancellationToken);
        return Created($"/api/admin/users/{user.Id}", MapUser(user, selectedRoles));
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> UpdateUser(Guid userId, UpdateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(userId, cancellationToken);
        await currentUser.EnsureBranchAccessAsync(request.BranchId, cancellationToken);
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == request.BranchId && x.CompanyId == currentUser.CompanyId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Seçilen şube aktif değil.");
        var selectedRoles = NormalizeRoles(request.Roles);
        EnsureCanAssign(selectedRoles);
        var employeeCode = Clean(request.EmployeeCode)?.ToUpperInvariant();
        if (employeeCode is not null && await userManager.Users.AnyAsync(x => x.Id != user.Id && x.CompanyId == currentUser.CompanyId && x.EmployeeCode == employeeCode, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Personel kodu kullanımda", "Bu personel kodu aynı firmada başka bir kullanıcıya ait.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = Clean(request.PhoneNumber);
        user.EmployeeCode = employeeCode;
        user.JobTitle = Clean(request.JobTitle);
        user.CompanyId = currentUser.CompanyId;
        user.BranchId = branch.Id;
        user.Branch = branch;
        user.IsActive = request.IsActive;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Kullanıcı güncellenemedi", string.Join(" ", updateResult.Errors.Select(x => x.Description)));

        var currentRoles = await userManager.GetRolesAsync(user);
        var remove = currentRoles.Where(x => !selectedRoles.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
        var add = selectedRoles.Where(x => !currentRoles.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (remove.Length > 0)
        {
            var result = await userManager.RemoveFromRolesAsync(user, remove);
            if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Roller güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (add.Length > 0)
        {
            var result = await userManager.AddToRolesAsync(user, add);
            if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Roller güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) throw new ApiException(StatusCodes.Status500InternalServerError, "Oturumlar yenilenemedi", "Yetki değişikliği sonrası eski oturumlar kapatılamadı.");
        await audit.RecordAsync(currentUser.UserId, branch.Id, "user.updated", "User", user.Id.ToString(), $"Kullanıcı bilgileri ve yetkileri güncellendi. Roller: {string.Join(", ", selectedRoles)}", cancellationToken: cancellationToken);
        return Ok(await MapDetailAsync(user));
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<ActionResult<AdminUserDto>> UpdateUserStatus(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(userId, cancellationToken);
        user.IsActive = request.IsActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) throw new ApiException(StatusCodes.Status400BadRequest, "Kullanıcı güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) throw new ApiException(StatusCodes.Status500InternalServerError, "Oturumlar yenilenemedi", "Hesap durumu değişikliği sonrası eski oturumlar kapatılamadı.");
        var roles = await userManager.GetRolesAsync(user);
        await audit.RecordAsync(currentUser.UserId, user.BranchId, request.IsActive ? "user.activated" : "user.deactivated", "User", user.Id.ToString(), request.IsActive ? "Kullanıcı erişimi açıldı." : "Kullanıcı erişimi kapatıldı.", cancellationToken: cancellationToken);
        return Ok(MapUser(user, roles));
    }

    private async Task<AppUser> FindManageableUserAsync(Guid userId, CancellationToken cancellationToken, bool allowSelf = false)
    {
        if (!allowSelf && userId == currentUser.UserId) throw new ApiException(StatusCodes.Status400BadRequest, "İşlem reddedildi", "Kendi hesabınızın rol veya aktiflik durumunu bu ekrandan değiştiremezsiniz.");
        var user = await userManager.Users.Include(x => x.Branch).SingleOrDefaultAsync(x => x.Id == userId && x.CompanyId == currentUser.CompanyId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Kullanıcı bulunamadı", "Kullanıcı hesabı firmanızda bulunamadı.");
        if (!currentUser.CanAccessAllBranches)
        {
            if (user.BranchId is not { } targetBranch || targetBranch != currentUser.ResolveBranchId(null))
                throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu kullanıcının şubesini yönetme yetkiniz bulunmuyor.");
        }
        var targetRoles = await userManager.GetRolesAsync(user);
        if (userId != currentUser.UserId && !Roles.CanManageTarget(currentUser.RoleNames, targetRoles))
            throw new ApiException(StatusCodes.Status403Forbidden, "Yetki yetersiz", "Bu kullanıcı sizin yetki seviyenizle yönetilemez.");
        return user;
    }

    private void EnsureCanAssign(IReadOnlyList<string> roles)
    {
        if (!Roles.CanAssignRoles(currentUser.RoleNames, roles))
            throw new ApiException(StatusCodes.Status403Forbidden, "Rol atanamaz", "Kendi yetki seviyenizle eşit veya daha yüksek bir rol atayamazsınız.");
    }

    private static IReadOnlyList<string> NormalizeRoles(IEnumerable<string> roles)
    {
        var selected = roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selected.Count == 0 || selected.Any(x => !Roles.All.Contains(x, StringComparer.OrdinalIgnoreCase)))
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol geçersiz", "Geçerli en az bir kullanıcı rolü seçin.");
        return selected;
    }

    private async Task<AdminUserDetailDto> MapDetailAsync(AppUser user) => new(
        user.Id, user.FullName, user.Email ?? string.Empty, user.PhoneNumber, user.EmployeeCode, user.JobTitle,
        user.BranchId, user.Branch?.Name, user.IsActive, user.TwoFactorEnabled, user.CreatedAt, user.LastLoginAt,
        (await userManager.GetRolesAsync(user)).ToList());

    private static AdminUserDto MapUser(AppUser user, IEnumerable<string> roles) => new(user.Id, user.FullName, user.Email ?? string.Empty, user.BranchId, user.Branch?.Name, user.IsActive, roles.ToList());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
