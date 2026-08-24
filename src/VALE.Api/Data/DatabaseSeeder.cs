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

        var company = await db.Companies.SingleAsync(x => x.Id == LegacyCompanyId, cancellationToken);
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
            await userManager.UpdateAsync(admin);
        }

        if (company.OwnerUserId is null)
        {
            company.OwnerUserId = admin.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

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

        var requiredRoles = new[] { Roles.Owner, Roles.Admin, Roles.OperationsManager, Roles.Manager };
        var existingRoles = await userManager.GetRolesAsync(admin);
        var missingRoles = requiredRoles.Where(role => !existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingRoles.Length > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(admin, missingRoles);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Yönetici rolleri atanamadı: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
        }

        logger.LogInformation("VALE şirket/tenant şeması, roller, merkez şube ve sistem sahibi doğrulandı.");
    }

    private static Task EnsureCompanySchemaAsync(ValeDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Companies" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Name" character varying(160) NOT NULL,
                "Code" character varying(40) NOT NULL,
                "OwnerUserId" uuid NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Companies_Code" ON "Companies" ("Code");
            INSERT INTO "Companies" ("Id", "Name", "Code", "IsActive", "CreatedAt")
            VALUES ('11111111-1111-1111-1111-111111111111', 'VALE', 'VALE', TRUE, now())
            ON CONFLICT ("Id") DO NOTHING;

            ALTER TABLE "Branches" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            ALTER TABLE "Branches" ADD COLUMN IF NOT EXISTS "InviteCode" character varying(40);
            UPDATE "Branches" SET "CompanyId" = '11111111-1111-1111-1111-111111111111' WHERE "CompanyId" IS NULL;
            ALTER TABLE "Branches" ALTER COLUMN "CompanyId" SET NOT NULL;
            DROP INDEX IF EXISTS "IX_Branches_Code";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Branches_CompanyId_Code" ON "Branches" ("CompanyId", "Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Branches_InviteCode" ON "Branches" ("InviteCode") WHERE "InviteCode" IS NOT NULL;

            ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            UPDATE "Customers" SET "CompanyId" = '11111111-1111-1111-1111-111111111111' WHERE "CompanyId" IS NULL;
            ALTER TABLE "Customers" ALTER COLUMN "CompanyId" SET NOT NULL;
            DROP INDEX IF EXISTS "IX_Customers_NormalizedPhone";
            CREATE INDEX IF NOT EXISTS "IX_Customers_CompanyId_NormalizedPhone" ON "Customers" ("CompanyId", "NormalizedPhone");

            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "Year" integer;
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "FuelType" character varying(30);
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "Transmission" character varying(30);
            ALTER TABLE "Vehicles" ADD COLUMN IF NOT EXISTS "PhotoBase64" text;
            UPDATE "Vehicles" SET "CompanyId" = '11111111-1111-1111-1111-111111111111' WHERE "CompanyId" IS NULL;
            ALTER TABLE "Vehicles" ALTER COLUMN "CompanyId" SET NOT NULL;
            DROP INDEX IF EXISTS "IX_Vehicles_NormalizedPlate";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Vehicles_CompanyId_NormalizedPlate" ON "Vehicles" ("CompanyId", "NormalizedPlate");

            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "EmployeeCode" character varying(40);
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "JobTitle" character varying(80);
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "PreferredTheme" character varying(20) NOT NULL DEFAULT 'System';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AccentTheme" character varying(20) NOT NULL DEFAULT 'Blue';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "ProfileColor" character varying(20) NOT NULL DEFAULT '#2563EB';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now();
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LastLoginAt" timestamp with time zone;
            UPDATE "AspNetUsers" u SET "CompanyId" = b."CompanyId" FROM "Branches" b WHERE u."CompanyId" IS NULL AND u."BranchId" = b."Id";
            CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_CompanyId" ON "AspNetUsers" ("CompanyId");
            DROP INDEX IF EXISTS "IX_AspNetUsers_EmployeeCode";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AspNetUsers_CompanyId_EmployeeCode" ON "AspNetUsers" ("CompanyId", "EmployeeCode") WHERE "EmployeeCode" IS NOT NULL;

            CREATE TABLE IF NOT EXISTS "UserBranchMemberships" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "BranchId" uuid NOT NULL,
                "IsPrimary" boolean NOT NULL DEFAULT FALSE,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "FK_UserBranchMemberships_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_UserBranchMemberships_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_UserBranchMemberships_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "UserBranchMemberships" ("Id", "CompanyId", "UserId", "BranchId", "IsPrimary", "IsActive", "CreatedAt")
            SELECT gen_random_uuid(), u."CompanyId", u."Id", u."BranchId", TRUE, TRUE, now()
            FROM "AspNetUsers" u
            WHERE u."CompanyId" IS NOT NULL AND u."BranchId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM "UserBranchMemberships" m WHERE m."UserId" = u."Id" AND m."BranchId" = u."BranchId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserBranchMemberships_UserId_BranchId" ON "UserBranchMemberships" ("UserId", "BranchId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserBranchMemberships_UserId_Primary" ON "UserBranchMemberships" ("UserId") WHERE "IsPrimary" = TRUE AND "IsActive" = TRUE;
            CREATE INDEX IF NOT EXISTS "IX_UserBranchMemberships_CompanyId_BranchId_IsActive" ON "UserBranchMemberships" ("CompanyId", "BranchId", "IsActive");
            CREATE INDEX IF NOT EXISTS "IX_UserBranchMemberships_CompanyId_UserId_IsActive" ON "UserBranchMemberships" ("CompanyId", "UserId", "IsActive");

            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "UpdatedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedByUserId" uuid;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone;
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "DeletedReason" character varying(300);
            ALTER TABLE "ParkingTickets" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            UPDATE "ParkingTickets" t SET "CompanyId" = b."CompanyId" FROM "Branches" b WHERE t."CompanyId" IS NULL AND t."BranchId" = b."Id";
            ALTER TABLE "ParkingTickets" ALTER COLUMN "CompanyId" SET NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_ParkingTickets_DeletedAt" ON "ParkingTickets" ("DeletedAt");
            DROP INDEX IF EXISTS "IX_ParkingTickets_BranchId_Status_EntryAt";
            CREATE INDEX IF NOT EXISTS "IX_ParkingTickets_CompanyId_BranchId_Status_EntryAt" ON "ParkingTickets" ("CompanyId", "BranchId", "Status", "EntryAt");

            ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            UPDATE "Payments" p SET "CompanyId" = t."CompanyId" FROM "ParkingTickets" t WHERE p."CompanyId" IS NULL AND p."TicketId" = t."Id";
            ALTER TABLE "Payments" ALTER COLUMN "CompanyId" SET NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_Payments_CompanyId_PaidAt" ON "Payments" ("CompanyId", "PaidAt");

            CREATE TABLE IF NOT EXISTS "AuditEntries" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
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
            ALTER TABLE "AuditEntries" ADD COLUMN IF NOT EXISTS "CompanyId" uuid;
            UPDATE "AuditEntries" a SET "CompanyId" = b."CompanyId" FROM "Branches" b WHERE a."CompanyId" IS NULL AND a."BranchId" = b."Id";
            UPDATE "AuditEntries" a SET "CompanyId" = u."CompanyId" FROM "AspNetUsers" u WHERE a."CompanyId" IS NULL AND a."UserId" = u."Id";
            UPDATE "AuditEntries" SET "CompanyId" = '11111111-1111-1111-1111-111111111111' WHERE "CompanyId" IS NULL;
            ALTER TABLE "AuditEntries" ALTER COLUMN "CompanyId" SET NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_OccurredAt" ON "AuditEntries" ("OccurredAt");
            DROP INDEX IF EXISTS "IX_AuditEntries_BranchId_OccurredAt";
            DROP INDEX IF EXISTS "IX_AuditEntries_UserId_OccurredAt";
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_CompanyId_OccurredAt" ON "AuditEntries" ("CompanyId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_CompanyId_BranchId_OccurredAt" ON "AuditEntries" ("CompanyId", "BranchId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_AuditEntries_CompanyId_UserId_OccurredAt" ON "AuditEntries" ("CompanyId", "UserId", "OccurredAt");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Branches_Companies_CompanyId' AND conrelid = '"Branches"'::regclass) THEN
                    ALTER TABLE "Branches" ADD CONSTRAINT "FK_Branches_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Customers_Companies_CompanyId' AND conrelid = '"Customers"'::regclass) THEN
                    ALTER TABLE "Customers" ADD CONSTRAINT "FK_Customers_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Vehicles_Companies_CompanyId' AND conrelid = '"Vehicles"'::regclass) THEN
                    ALTER TABLE "Vehicles" ADD CONSTRAINT "FK_Vehicles_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AspNetUsers_Companies_CompanyId' AND conrelid = '"AspNetUsers"'::regclass) THEN
                    ALTER TABLE "AspNetUsers" ADD CONSTRAINT "FK_AspNetUsers_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ParkingTickets_Companies_CompanyId' AND conrelid = '"ParkingTickets"'::regclass) THEN
                    ALTER TABLE "ParkingTickets" ADD CONSTRAINT "FK_ParkingTickets_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_Companies_CompanyId' AND conrelid = '"Payments"'::regclass) THEN
                    ALTER TABLE "Payments" ADD CONSTRAINT "FK_Payments_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AuditEntries_Companies_CompanyId' AND conrelid = '"AuditEntries"'::regclass) THEN
                    ALTER TABLE "AuditEntries" ADD CONSTRAINT "FK_AuditEntries_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
            END $$;

            CREATE TABLE IF NOT EXISTS "RegistrationRequests" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
                "BranchId" uuid NOT NULL,
                "ApplicantUserId" uuid NOT NULL,
                "RequestedRole" character varying(40) NOT NULL,
                "Status" character varying(20) NOT NULL,
                "ReviewedByUserId" uuid NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "Note" character varying(500) NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RegistrationRequests_ApplicantUserId" ON "RegistrationRequests" ("ApplicantUserId");
            CREATE INDEX IF NOT EXISTS "IX_RegistrationRequests_CompanyId_Status_CreatedAt" ON "RegistrationRequests" ("CompanyId", "Status", "CreatedAt");

            CREATE TABLE IF NOT EXISTS "Notifications" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
                "BranchId" uuid NULL,
                "UserId" uuid NOT NULL,
                "Title" character varying(140) NOT NULL,
                "Body" character varying(600) NOT NULL,
                "Type" character varying(40) NOT NULL,
                "IsRead" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_IsRead_CreatedAt" ON "Notifications" ("UserId", "IsRead", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_Notifications_CompanyId_CreatedAt" ON "Notifications" ("CompanyId", "CreatedAt");

            CREATE TABLE IF NOT EXISTS "PushRegistrations" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Token" character varying(4096) NOT NULL,
                "Platform" character varying(32) NOT NULL,
                "DeviceName" character varying(120) NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "LastSeenAt" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PushRegistrations_Token" ON "PushRegistrations" ("Token");
            CREATE INDEX IF NOT EXISTS "IX_PushRegistrations_UserId_IsActive_LastSeenAt" ON "PushRegistrations" ("UserId", "IsActive", "LastSeenAt");
            CREATE INDEX IF NOT EXISTS "IX_PushRegistrations_CompanyId_UserId" ON "PushRegistrations" ("CompanyId", "UserId");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RegistrationRequests_Companies_CompanyId' AND conrelid = '"RegistrationRequests"'::regclass) THEN
                    ALTER TABLE "RegistrationRequests" ADD CONSTRAINT "FK_RegistrationRequests_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Notifications_Companies_CompanyId' AND conrelid = '"Notifications"'::regclass) THEN
                    ALTER TABLE "Notifications" ADD CONSTRAINT "FK_Notifications_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PushRegistrations_Companies_CompanyId' AND conrelid = '"PushRegistrations"'::regclass) THEN
                    ALTER TABLE "PushRegistrations" ADD CONSTRAINT "FK_PushRegistrations_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
            """,
            cancellationToken);
}
