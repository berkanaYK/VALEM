using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Domain;

namespace VALE.Api.Data;

public static class DatabaseSeeder
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<ValeDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await db.Database.EnsureCreatedAsync(cancellationToken);

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"'{roleName}' rolü oluşturulamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning(
                "İlk yönetici oluşturulmadı. Seed:AdminEmail ve Seed:AdminPassword güvenli yapılandırmada tanımlanmalı.");
            return;
        }

        var branchCode = options.DefaultBranchCode.Trim().ToUpperInvariant();
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Code == branchCode, cancellationToken);
        if (branch is null)
        {
            branch = new Branch
            {
                Code = branchCode,
                Name = options.DefaultBranchName.Trim(),
                City = options.DefaultBranchCity.Trim()
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingUser = await userManager.FindByEmailAsync(options.AdminEmail.Trim());
        if (existingUser is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = options.AdminEmail.Trim(),
            Email = options.AdminEmail.Trim(),
            EmailConfirmed = true,
            FullName = options.AdminFullName.Trim(),
            BranchId = branch.Id,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(admin, options.AdminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"İlk yönetici oluşturulamadı: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
        }

        var roleResultForUser = await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.Manager]);
        if (!roleResultForUser.Succeeded)
        {
            throw new InvalidOperationException(
                $"Yönetici rolleri atanamadı: {string.Join(", ", roleResultForUser.Errors.Select(x => x.Description))}");
        }

        logger.LogInformation("İlk VALE yöneticisi ve merkez şube oluşturuldu.");
    }
}

