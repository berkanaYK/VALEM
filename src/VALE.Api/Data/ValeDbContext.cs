using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Domain;

namespace VALE.Api.Data;

public sealed class ValeDbContext(DbContextOptions<ValeDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingTicket> ParkingTickets => Set<ParkingTicket>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(300);
        });

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(120);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => x.NormalizedPhone);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.NormalizedPhone).HasMaxLength(30);
        });

        builder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(x => x.NormalizedPlate).IsUnique();
            entity.Property(x => x.LicensePlate).HasMaxLength(16);
            entity.Property(x => x.NormalizedPlate).HasMaxLength(16);
            entity.Property(x => x.Brand).HasMaxLength(60);
            entity.Property(x => x.Model).HasMaxLength(60);
            entity.Property(x => x.Color).HasMaxLength(40);
        });

        builder.Entity<ParkingTicket>(entity =>
        {
            entity.HasIndex(x => x.TicketNumber).IsUnique();
            entity.HasIndex(x => new { x.BranchId, x.Status, x.EntryAt });
            entity.Property(x => x.TicketNumber).HasMaxLength(40);
            entity.Property(x => x.KeyTag).HasMaxLength(30);
            entity.Property(x => x.ParkingSpot).HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.HourlyRate).HasPrecision(12, 2);
            entity.Property(x => x.AmountDue).HasPrecision(12, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2);
            entity.HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(x => x.RecordedByUser)
                .WithMany()
                .HasForeignKey(x => x.RecordedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
