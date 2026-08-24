using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Domain;

namespace VALE.Api.Data;

public sealed class ValeDbContext(DbContextOptions<ValeDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserBranchMembership> UserBranchMemberships => Set<UserBranchMembership>();
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<ValeNotification> Notifications => Set<ValeNotification>();
    public DbSet<PushRegistration> PushRegistrations => Set<PushRegistration>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingTicket> ParkingTickets => Set<ParkingTicket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public override int SaveChanges() =>
        throw new InvalidOperationException("Tenant bütünlüğü doğrulaması için SaveChangesAsync kullanılmalıdır.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Tenant bütünlüğü doğrulaması için SaveChangesAsync kullanılmalıdır.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ApplyTenantDefaultsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ApplyTenantDefaultsAsync(CancellationToken cancellationToken)
    {
        var users = ChangeTracker.Entries<AppUser>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => x.Entity)
            .Where(x => x.BranchId.HasValue)
            .ToList();

        foreach (var user in users)
        {
            var branchCompanyId = user.Branch is { CompanyId: var companyId } && companyId != Guid.Empty
                ? companyId
                : await Branches.AsNoTracking()
                .Where(x => x.Id == user.BranchId!.Value)
                .Select(x => (Guid?)x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!branchCompanyId.HasValue)
                throw new InvalidOperationException("Kullanıcının varsayılan şubesi bulunamadı.");
            if (!user.CompanyId.HasValue) user.CompanyId = branchCompanyId.Value;
            if (user.CompanyId.Value != branchCompanyId.Value)
                throw new InvalidOperationException("Kullanıcı ve varsayılan şube farklı firmalara ait olamaz.");
        }

        foreach (var ticketEntry in ChangeTracker.Entries<ParkingTicket>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var ticket = ticketEntry.Entity;
            var branchCompanyId = ticket.Branch is { CompanyId: var trackedCompanyId } && trackedCompanyId != Guid.Empty
                ? trackedCompanyId
                : await Branches.AsNoTracking().Where(x => x.Id == ticket.BranchId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!branchCompanyId.HasValue)
                throw new InvalidOperationException("Vale kaydının şubesi bulunamadı.");
            if (ticket.CompanyId == Guid.Empty) ticket.CompanyId = branchCompanyId.Value;
            if (ticket.CompanyId != branchCompanyId.Value)
                throw new InvalidOperationException("Vale kaydı ve şube farklı firmalara ait olamaz.");

            var vehicleCompanyId = ticket.Vehicle is { CompanyId: var vehicleTenant } && vehicleTenant != Guid.Empty
                ? vehicleTenant
                : await Vehicles.AsNoTracking().Where(x => x.Id == ticket.VehicleId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!vehicleCompanyId.HasValue || vehicleCompanyId.Value != ticket.CompanyId)
                throw new InvalidOperationException("Vale kaydı ve araç farklı firmalara ait olamaz.");

            if (ticket.CustomerId.HasValue || ticket.Customer is not null)
            {
                var customerCompanyId = ticket.Customer is { CompanyId: var customerTenant } && customerTenant != Guid.Empty
                    ? customerTenant
                    : await Customers.AsNoTracking().Where(x => x.Id == ticket.CustomerId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
                if (!customerCompanyId.HasValue || customerCompanyId.Value != ticket.CompanyId)
                    throw new InvalidOperationException("Vale kaydı ve müşteri farklı firmalara ait olamaz.");
            }

            var relatedUserIds = new[] { ticket.AssignedUserId, ticket.CreatedByUserId, ticket.UpdatedByUserId, ticket.DeletedByUserId }
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
            if (relatedUserIds.Length > 0)
            {
                var matchingUsers = await Users.AsNoTracking().CountAsync(
                    x => relatedUserIds.Contains(x.Id) && x.CompanyId == ticket.CompanyId,
                    cancellationToken);
                if (matchingUsers != relatedUserIds.Length)
                    throw new InvalidOperationException("Vale kaydındaki personel ilişkileri aynı firmaya ait olmalıdır.");
            }
        }

        foreach (var paymentEntry in ChangeTracker.Entries<Payment>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var payment = paymentEntry.Entity;
            var ticketCompanyId = payment.Ticket is { CompanyId: var trackedCompanyId } && trackedCompanyId != Guid.Empty
                ? trackedCompanyId
                : await ParkingTickets.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == payment.TicketId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!ticketCompanyId.HasValue)
                throw new InvalidOperationException("Ödemenin vale kaydı bulunamadı.");
            if (payment.CompanyId == Guid.Empty) payment.CompanyId = ticketCompanyId.Value;
            if (payment.CompanyId != ticketCompanyId.Value)
                throw new InvalidOperationException("Ödeme ve vale kaydı farklı firmalara ait olamaz.");
            if (payment.RecordedByUserId.HasValue)
            {
                var recorderAllowed = await Users.AsNoTracking().AnyAsync(
                    x => x.Id == payment.RecordedByUserId.Value && x.CompanyId == payment.CompanyId,
                    cancellationToken);
                if (!recorderAllowed)
                    throw new InvalidOperationException("Ödemeyi kaydeden kullanıcı farklı bir firmaya ait olamaz.");
            }
        }

        foreach (var membershipEntry in ChangeTracker.Entries<UserBranchMembership>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var membership = membershipEntry.Entity;
            var branchCompanyId = membership.Branch is { CompanyId: var trackedBranchCompanyId } && trackedBranchCompanyId != Guid.Empty
                ? trackedBranchCompanyId
                : await Branches.AsNoTracking().Where(x => x.Id == membership.BranchId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            var userCompanyId = membership.User is { CompanyId: var trackedUserCompanyId } && trackedUserCompanyId.HasValue
                ? trackedUserCompanyId
                : await Users.AsNoTracking().Where(x => x.Id == membership.UserId).Select(x => x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!branchCompanyId.HasValue || !userCompanyId.HasValue || branchCompanyId.Value != userCompanyId.Value)
                throw new InvalidOperationException("Kullanıcı yalnızca kendi firmasındaki şube gruplarına eklenebilir.");
            if (membership.CompanyId == Guid.Empty) membership.CompanyId = branchCompanyId.Value;
            if (membership.CompanyId != branchCompanyId.Value)
                throw new InvalidOperationException("Şube üyeliğinin firma kapsamı geçersiz.");
        }

        foreach (var registrationEntry in ChangeTracker.Entries<RegistrationRequest>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var registration = registrationEntry.Entity;
            var branchCompanyId = registration.Branch is { CompanyId: var trackedBranchCompanyId } && trackedBranchCompanyId != Guid.Empty
                ? trackedBranchCompanyId
                : await Branches.AsNoTracking().Where(x => x.Id == registration.BranchId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            var applicantCompanyId = registration.ApplicantUser is { CompanyId: var trackedApplicantCompanyId } && trackedApplicantCompanyId.HasValue
                ? trackedApplicantCompanyId
                : await Users.AsNoTracking().Where(x => x.Id == registration.ApplicantUserId).Select(x => x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!branchCompanyId.HasValue || !applicantCompanyId.HasValue ||
                branchCompanyId.Value != registration.CompanyId || applicantCompanyId.Value != registration.CompanyId)
                throw new InvalidOperationException("Kayıt başvurusu, kullanıcı ve şube aynı firmaya ait olmalıdır.");
            if (registration.ReviewedByUserId.HasValue)
            {
                var reviewerAllowed = await Users.AsNoTracking().AnyAsync(
                    x => x.Id == registration.ReviewedByUserId.Value && x.CompanyId == registration.CompanyId,
                    cancellationToken);
                if (!reviewerAllowed)
                    throw new InvalidOperationException("Başvuruyu inceleyen kullanıcı farklı bir firmaya ait olamaz.");
            }
        }

        foreach (var notificationEntry in ChangeTracker.Entries<ValeNotification>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var notification = notificationEntry.Entity;
            var userCompanyId = notification.User is { CompanyId: var trackedUserCompanyId } && trackedUserCompanyId.HasValue
                ? trackedUserCompanyId
                : await Users.AsNoTracking().Where(x => x.Id == notification.UserId).Select(x => x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!userCompanyId.HasValue || userCompanyId.Value != notification.CompanyId)
                throw new InvalidOperationException("Bildirim yalnızca aynı firmadaki kullanıcıya gönderilebilir.");
            if (notification.BranchId.HasValue)
            {
                var branchCompanyId = await Branches.AsNoTracking().Where(x => x.Id == notification.BranchId.Value).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
                if (!branchCompanyId.HasValue || branchCompanyId.Value != notification.CompanyId)
                    throw new InvalidOperationException("Bildirim şubesi ve kullanıcı farklı firmalara ait olamaz.");
            }
        }

        foreach (var pushEntry in ChangeTracker.Entries<PushRegistration>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var push = pushEntry.Entity;
            var userCompanyId = push.User is { CompanyId: var trackedUserCompanyId } && trackedUserCompanyId.HasValue
                ? trackedUserCompanyId
                : await Users.AsNoTracking().Where(x => x.Id == push.UserId).Select(x => x.CompanyId).SingleOrDefaultAsync(cancellationToken);
            if (!userCompanyId.HasValue || userCompanyId.Value != push.CompanyId)
                throw new InvalidOperationException("Push kaydı yalnızca aynı firmadaki kullanıcıya bağlanabilir.");
        }

        foreach (var auditEntry in ChangeTracker.Entries<AuditEntry>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var audit = auditEntry.Entity;
            if (audit.CompanyId == Guid.Empty)
                throw new InvalidOperationException("Denetim kaydında firma kapsamı zorunludur.");
            if (audit.BranchId.HasValue)
            {
                var branchCompanyId = await Branches.AsNoTracking().Where(x => x.Id == audit.BranchId.Value).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
                if (!branchCompanyId.HasValue || branchCompanyId.Value != audit.CompanyId)
                    throw new InvalidOperationException("Denetim kaydı ve şube farklı firmalara ait olamaz.");
            }
            if (audit.UserId.HasValue)
            {
                var userCompanyId = await Users.AsNoTracking().Where(x => x.Id == audit.UserId.Value).Select(x => x.CompanyId).SingleOrDefaultAsync(cancellationToken);
                if (!userCompanyId.HasValue || userCompanyId.Value != audit.CompanyId)
                    throw new InvalidOperationException("Denetim kaydı ve kullanıcı farklı firmalara ait olamaz.");
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.Name).HasMaxLength(160);
        });

        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasIndex(x => x.InviteCode).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.InviteCode).HasMaxLength(40);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(120);
            entity.Property(x => x.EmployeeCode).HasMaxLength(40);
            entity.Property(x => x.JobTitle).HasMaxLength(80);
            entity.Property(x => x.PreferredTheme).HasMaxLength(20);
            entity.Property(x => x.AccentTheme).HasMaxLength(20);
            entity.Property(x => x.ProfileColor).HasMaxLength(20);
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeCode }).IsUnique();
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserBranchMembership>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.BranchId }).IsUnique();
            entity.HasIndex(x => x.UserId).HasDatabaseName("IX_UserBranchMemberships_UserId_Primary")
                .IsUnique().HasFilter("\"IsPrimary\" = TRUE AND \"IsActive\" = TRUE");
            entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.IsActive });
            entity.HasIndex(x => new { x.CompanyId, x.UserId, x.IsActive });
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(x => x.BranchMemberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany(x => x.UserMemberships).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RegistrationRequest>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.CreatedAt });
            entity.HasIndex(x => x.ApplicantUserId).IsUnique();
            entity.Property(x => x.RequestedRole).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApplicantUser).WithMany().HasForeignKey(x => x.ApplicantUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ValeNotification>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            entity.HasIndex(x => new { x.CompanyId, x.CreatedAt });
            entity.Property(x => x.Title).HasMaxLength(140);
            entity.Property(x => x.Body).HasMaxLength(600);
            entity.Property(x => x.Type).HasMaxLength(40);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PushRegistration>(entity =>
        {
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsActive, x.LastSeenAt });
            entity.HasIndex(x => new { x.CompanyId, x.UserId });
            entity.Property(x => x.Token).HasMaxLength(4096);
            entity.Property(x => x.Platform).HasMaxLength(32);
            entity.Property(x => x.DeviceName).HasMaxLength(120);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.NormalizedPhone });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.NormalizedPhone).HasMaxLength(30);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.NormalizedPlate }).IsUnique();
            entity.Property(x => x.LicensePlate).HasMaxLength(16);
            entity.Property(x => x.NormalizedPlate).HasMaxLength(16);
            entity.Property(x => x.Brand).HasMaxLength(60);
            entity.Property(x => x.Model).HasMaxLength(60);
            entity.Property(x => x.Color).HasMaxLength(40);
            entity.Property(x => x.FuelType).HasMaxLength(30);
            entity.Property(x => x.Transmission).HasMaxLength(30);
            entity.Property(x => x.PhotoBase64).HasColumnType("text");
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ParkingTicket>(entity =>
        {
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.HasIndex(x => x.TicketNumber).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.Status, x.EntryAt });
            entity.Property(x => x.TicketNumber).HasMaxLength(40);
            entity.Property(x => x.KeyTag).HasMaxLength(30);
            entity.Property(x => x.ParkingSpot).HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.DeletedReason).HasMaxLength(300);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.HourlyRate).HasPrecision(12, 2);
            entity.Property(x => x.AmountDue).HasPrecision(12, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.PaidAt });
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditEntry>(entity =>
        {
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.CompanyId, x.OccurredAt });
            entity.HasIndex(x => new { x.CompanyId, x.BranchId, x.OccurredAt });
            entity.HasIndex(x => new { x.CompanyId, x.UserId, x.OccurredAt });
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Detail).HasMaxLength(1000);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
