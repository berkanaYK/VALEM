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
        await EnsureCompanySchemaAsync(db, cancellationToken);

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException($"'{roleName}' rolü oluşturulamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("İlk yönetici oluşturulmadı. Seed:AdminEmail ve Seed:AdminPassword güvenli yapılandırmada tanımlanmalı.");
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
            admin.BranchId ??= branch.Id;
            admin.JobTitle ??= "Sistem Sahibi";
            await userManager.UpdateAsync(admin);
        }

        var requiredRoles = new[] { Roles.Owner, Roles.Admin, Roles.OperationsManager, Roles.Manager };
        var existingRoles = await userManager.GetRolesAsync(admin);
        var missingRoles = requiredRoles.Where(role => !existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingRoles.Length > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(admin, missingRoles);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Yönetici rolleri atanamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
        }

        logger.LogInformation("VALE şirket rolleri, merkez şube ve sistem sahibi doğrulandı.");
    }

    private static Task EnsureCompanySchemaAsync(ValeDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "Year" integer;
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "FuelType" character varying(30);
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "Transmission" character varying(30);
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "PhotoBase64" text;

            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "EmployeeCode" character varying(40);
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "JobTitle" character varying(80);
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "PreferredTheme" character varying(20) NOT NULL DEFAULT 'System';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AccentTheme" character varying(20) NOT NULL DEFAULT 'Blue';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "ProfileColor" character varying(20) NOT NULL DEFAULT '#2563EB';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now();
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LastLoginAt" timestamp with time zone;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AspNetUsers_EmployeeCode" ON "AspNetUsers" ("EmployeeCode") WHERE "EmployeeCode" IS NOT NULL;

            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "UpdatedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedReason" character varying(300);
            CREATE INDEX IF NOT EXISTS "IX_ParkingTickets_DeletedAt" ON "ParkingTickets" ("DeletedAt");

            CREATE TABLE IF NOT EXISTS "AuditEntries" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "UserId" uuid NULL,
                "BranchId" uuid NULL,
                "Action" character varying(80) NOT NULL,
                "EntityType" character varying(80) NOT NULL,
                "EntityId" character varying(80) NULL,
                "Detail" character varying(1000) NOT NULL,
                "IpAddress" character varying(64) NULL,
                "Success" boolean NOT NULL DEFAULT TRUE,
                "OccurredAt" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_OccurredAt" ON "AuditEntries" ("OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_BranchId_OccurredAt" ON "AuditEntries" ("BranchId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_UserId_OccurredAt" ON "AuditEntries" ("UserId", "OccurredAt");
            """,
            cancellationToken);
}
