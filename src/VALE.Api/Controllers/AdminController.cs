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
[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public sealed class AdminController(
    ValeDbContext db,
    UserManager<AppUser> userManager,
    CurrentUserContext currentUser) : ControllerBase
{
    [HttpPost("branches")]
    public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Şube kodu kullanımda", "Bu şube kodu daha önce kullanılmış.");
        }

        var branch = new Branch
        {
            Code = code,
            Name = request.Name.Trim(),
            City = request.City.Trim(),
            Address = request.Address?.Trim() ?? string.Empty,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/branches/{branch.Id}", new BranchDto(branch.Id, branch.Code, branch.Name, branch.City, branch.Address, branch.IsActive));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().Include(x => x.Branch).OrderBy(x => x.IsActive).ThenBy(x => x.FullName).ToListAsync(cancellationToken);
        var result = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(MapUser(user, await userManager.GetRolesAsync(user)));
        }
        return Ok(result);
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == request.BranchId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Kullanıcı için seçilen şube aktif değil.");

        var selectedRoles = request.Roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selectedRoles.Count == 0 || selectedRoles.Any(x => !Roles.All.Contains(x, StringComparer.OrdinalIgnoreCase)))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol geçersiz", "Geçerli en az bir kullanıcı rolü seçin.");
        }
        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "E-posta kullanımda", "Bu e-posta adresine ait kullanıcı zaten var.");
        }

        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            BranchId = branch.Id,
            Branch = branch,
            IsActive = true
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Kullanıcı oluşturulamadı", string.Join(" ", createResult.Errors.Select(x => x.Description)));
        }
        var roleResult = await userManager.AddToRolesAsync(user, selectedRoles);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ApiException(StatusCodes.Status400BadRequest, "Rol atanamadı", string.Join(" ", roleResult.Errors.Select(x => x.Description)));
        }
        return Created($"/api/admin/users/{user.Id}", MapUser(user, selectedRoles));
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<ActionResult<AdminUserDto>> UpdateUserStatus(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (userId == currentUser.UserId && !request.IsActive)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "İşlem reddedildi", "Kendi yönetici hesabınızı devre dışı bırakamazsınız.");
        }

        var user = await userManager.Users.Include(x => x.Branch).SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Kullanıcı bulunamadı", "Kullanıcı hesabı bulunamadı.");
        user.IsActive = request.IsActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Kullanıcı güncellenemedi", string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        return Ok(MapUser(user, await userManager.GetRolesAsync(user)));
    }

    private static AdminUserDto MapUser(AppUser user, IEnumerable<string> roles) => new(
        user.Id, user.FullName, user.Email ?? string.Empty, user.BranchId, user.Branch?.Name, user.IsActive, roles.ToList());
}
