using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Domain;

namespace VALE.Api.Data;

public static class DatabaseSeeder
{
    private static readonly Guid LegacyCompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<ValeDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var platformOptions = services.GetRequiredService<IOptions<PlatformAdminOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await DatabaseMigrator.MigrateAsync(db, logger, cancellationToken);

        foreach (var roleName in Roles.SeedRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"'{roleName}' rolü oluşturulamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
        }

        await EnsurePlatformAdminAsync(userManager, platformOptions, logger);
        var company = await EnsureLegacyCompanyAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("İlk firma yöneticisi oluşturulmadı. Seed:AdminEmail ve Seed:AdminPassword güvenli yapılandırmada tanımlanmalı.");
            return;
        }

        var branchCode = options.DefaultBranchCode.Trim().ToUpperInvariant();
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == branchCode, cancellationToken);
        if (branch is null)
        {
            branch = new Branch
            {
                CompanyId = company.Id,
                Company = company,
                Code = branchCode,
                Name = options.DefaultBranchName.Trim(),
                City = options.DefaultBranchCity.Trim(),
                InviteCode = $"{company.Code}-{branchCode}"
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync(cancellationToken);
        }

        var email = options.AdminEmail.Trim();
        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = options.AdminFullName.Trim(),
                CompanyId = company.Id,
                BranchId = branch.Id,
                IsActive = true,
                JobTitle = "Sistem Sahibi"
            };
            var createResult = await userManager.CreateAsync(admin, options.AdminPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"İlk yönetici oluşturulamadı: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
        }
        else
        {
            admin.IsActive = true;
            admin.CompanyId ??= company.Id;
            admin.BranchId ??= branch.Id;
            admin.JobTitle ??= "Sistem Sahibi";
            var updateResult = await userManager.UpdateAsync(admin);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException($"İlk yönetici güncellenemedi: {string.Join(", ", updateResult.Errors.Select(x => x.Description))}");
        }

        if (company.OwnerUserId is null)
        {
            company.OwnerUserId = admin.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsurePrimaryMembershipAsync(db, company, branch, admin, cancellationToken);
        var requiredRoles = new[] { Roles.Owner, Roles.Admin, Roles.OperationsManager, Roles.Manager };
        var existingRoles = await userManager.GetRolesAsync(admin);
        var missingRoles = requiredRoles.Where(role => !existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingRoles.Length > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(admin, missingRoles);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Yönetici rolleri atanamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
        }

        logger.LogInformation("VALEM migration'ları, roller, merkez şube ve sistem sahibi doğrulandı.");
    }

    private static async Task<Company> EnsureLegacyCompanyAsync(ValeDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Id == LegacyCompanyId, cancellationToken);
        if (company is not null) return company;
        company = new Company { Id = LegacyCompanyId, Name = "VALE", Code = "VALE", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        return company;
    }

    private static async Task EnsurePrimaryMembershipAsync(
        ValeDbContext db,
        Company company,
        Branch branch,
        AppUser admin,
        CancellationToken cancellationToken)
    {
        var primaryMembership = await db.UserBranchMemberships.SingleOrDefaultAsync(
            x => x.CompanyId == company.Id && x.UserId == admin.Id && x.BranchId == branch.Id,
            cancellationToken);
        if (primaryMembership is null)
        {
            db.UserBranchMemberships.Add(new UserBranchMembership
            {
                CompanyId = company.Id,
                Company = company,
                UserId = admin.Id,
                User = admin,
                BranchId = branch.Id,
                Branch = branch,
                IsPrimary = true,
                IsActive = true
            });
        }
        else
        {
            primaryMembership.IsPrimary = true;
            primaryMembership.IsActive = true;
            primaryMembership.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePlatformAdminAsync(
        UserManager<AppUser> userManager,
        PlatformAdminOptions options,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("Web platform yönetimi hesabı oluşturulmadı. PlatformAdmin:Email ve PlatformAdmin:Password güvenli yapılandırmada tanımlanmalı.");
            return;
        }

        var email = options.Email.Trim();
        var admin = await userManager.FindByEmailAsync(email);
        if (admin is not null && (admin.CompanyId.HasValue || admin.BranchId.HasValue))
            throw new InvalidOperationException("PlatformAdmin:Email mevcut bir firma kullanıcısına ait olamaz. Ayrı bir geliştirici e-posta adresi kullanın.");

        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = string.IsNullOrWhiteSpace(options.FullName) ? "VALEM Platform Yöneticisi" : options.FullName.Trim(),
                CompanyId = null,
                BranchId = null,
                IsActive = true,
                JobTitle = "Platform Yöneticisi"
            };
            var created = await userManager.CreateAsync(admin, options.Password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Platform yöneticisi oluşturulamadı: {string.Join(", ", created.Errors.Select(x => x.Description))}");
        }
        else
        {
            admin.IsActive = true;
            admin.EmailConfirmed = true;
            admin.FullName = string.IsNullOrWhiteSpace(options.FullName) ? admin.FullName : options.FullName.Trim();
            var updated = await userManager.UpdateAsync(admin);
            if (!updated.Succeeded)
                throw new InvalidOperationException($"Platform yöneticisi güncellenemedi: {string.Join(", ", updated.Errors.Select(x => x.Description))}");
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.PlatformAdmin))
        {
            var role = await userManager.AddToRoleAsync(admin, Roles.PlatformAdmin);
            if (!role.Succeeded)
                throw new InvalidOperationException($"Platform yöneticisi rolü atanamadı: {string.Join(", ", role.Errors.Select(x => x.Description))}");
        }

        logger.LogInformation("Web platform yönetimi hesabı doğrulandı: {Email}", email);
    }
}
