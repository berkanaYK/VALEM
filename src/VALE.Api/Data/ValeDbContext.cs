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
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<ValeNotification> Notifications => Set<ValeNotification>();
    public DbSet<PushRegistration> PushRegistrations => Set<PushRegistration>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingTicket> ParkingTickets => Set<ParkingTicket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTenantDefaultsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyTenantDefaultsAsync(CancellationToken cancellationToken)
    {
        var users = ChangeTracker.Entries<AppUser>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => x.Entity)
            .Where(x => x.CompanyId is null && x.BranchId.HasValue)
            .ToList();

        foreach (var user in users)
        {
            if (user.Branch is { CompanyId: var companyId } && companyId != Guid.Empty)
            {
                user.CompanyId = companyId;
                continue;
            }

            var branchId = user.BranchId!.Value;
            user.CompanyId = await Branches.AsNoTracking()
                .Where(x => x.Id == branchId)
                .Select(x => (Guid?)x.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
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
            entity.HasIndex(x => x.EmployeeCode).IsUnique();
            entity.HasIndex(x => x.CompanyId);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
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
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.NormalizedPhone });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.NormalizedPhone).HasMaxLength(30);
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
        });

        builder.Entity<ParkingTicket>(entity =>
        {
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.HasIndex(x => x.TicketNumber).IsUnique();
            entity.HasIndex(x => new { x.BranchId, x.Status, x.EntryAt });
            entity.Property(x => x.TicketNumber).HasMaxLength(40);
            entity.Property(x => x.KeyTag).HasMaxLength(30);
            entity.Property(x => x.ParkingSpot).HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.DeletedReason).HasMaxLength(300);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.HourlyRate).HasPrecision(12, 2);
            entity.Property(x => x.AmountDue).HasPrecision(12, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2);
            entity.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditEntry>(entity =>
        {
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.BranchId, x.OccurredAt });
            entity.HasIndex(x => new { x.UserId, x.OccurredAt });
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Detail).HasMaxLength(1000);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
