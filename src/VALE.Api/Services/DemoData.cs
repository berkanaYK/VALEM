using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Contracts;

namespace VALE.Api.Services;

public static class DemoData
{
    public static readonly Guid CompanyId = Guid.Parse("a29c8f44-0125-413d-9a41-a4a8455635a0");
    public static readonly Guid UserId = Guid.Parse("e3f1e2d9-76dd-45b9-bb67-2c92dc71c11b");

    public static async Task EnsureAsync(ValeDbContext db, UserManager<AppUser> users)
    {
        if (await db.Companies.AnyAsync(x => x.Id == CompanyId)) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var company = new Company { Id = CompanyId, Code = "VALEM-DEMO", Name = "Örnek Vale İşletmesi • Deneme", IsDemo = true };
        var branch = new Branch { Company = company, CompanyId = company.Id, Name = "Örnek Merkez", Code = "DEMO", City = "İstanbul" };
        db.Companies.Add(company); db.Branches.Add(branch);
        await db.SaveChangesAsync();
        var user = new AppUser { Id = UserId, UserName = "preview@vale.invalid", Email = "preview@vale.invalid", FullName = "Deneme Kullanıcısı", CompanyId = company.Id, BranchId = branch.Id, Branch = branch, EmailConfirmed = false };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Deneme hesabı oluşturulamadı.");
        result = await users.AddToRoleAsync(user, Roles.Owner);
        if (!result.Succeeded) throw new InvalidOperationException("Deneme yetkileri hazırlanamadı.");
        company.OwnerUserId = user.Id;
        db.UserBranchMemberships.Add(new UserBranchMembership { UserId = user.Id, CompanyId = company.Id, BranchId = branch.Id, IsPrimary = true });
        foreach (var (plate, brand, model, spot, status) in new[]
        {
            ("34 DEM 001", "Renault", "Clio", "A-01", TicketStatus.Parked),
            ("34 DEM 002", "Toyota", "Corolla", "A-02", TicketStatus.Requested),
            ("34 DEM 003", "Volkswagen", "Golf", "B-01", TicketStatus.Received)
        })
        {
            var vehicle = new Vehicle { CompanyId = company.Id, LicensePlate = plate, NormalizedPlate = plate.Replace(" ", ""), Brand = brand, Model = model, Color = "Gri" };
            db.Vehicles.Add(vehicle);
            db.ParkingTickets.Add(new ParkingTicket { Company = company, CompanyId = company.Id, Branch = branch, BranchId = branch.Id, Vehicle = vehicle, VehicleId = vehicle.Id, TicketNumber = $"DEMO-{spot}", AssignedUserId = user.Id, CreatedByUserId = user.Id, ParkingSpot = spot, Status = status, HourlyRate = 100m, EntryAt = DateTimeOffset.UtcNow.AddMinutes(-35), Notes = "Tanıtım için hazırlanmış örnek kayıttır." });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
