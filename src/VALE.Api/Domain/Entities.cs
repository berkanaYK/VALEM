using Microsoft.AspNetCore.Identity;
using VALE.Contracts;

namespace VALE.Api.Domain;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Branch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
    public string? EmployeeCode { get; set; }
    public string? JobTitle { get; set; }
    public string PreferredTheme { get; set; } = "System";
    public string AccentTheme { get; set; } = "Blue";
    public string ProfileColor { get; set; } = "#2563EB";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NormalizedPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string LicensePlate { get; set; } = string.Empty;
    public string NormalizedPlate { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public int? Year { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public string? PhotoBase64 { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ParkingTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? AssignedUserId { get; set; }
    public AppUser? AssignedUser { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public AppUser? DeletedByUser { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedReason { get; set; }
    public string? KeyTag { get; set; }
    public string? ParkingSpot { get; set; }
    public string? Notes { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Received;
    public DateTimeOffset EntryAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset? ExitAt { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal PaidAmount { get; set; }
    public List<Payment> Payments { get; set; } = [];
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public ParkingTicket Ticket { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
    public AppUser? RecordedByUser { get; set; }
}

public sealed class RegistrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string RequestedRole { get; set; } = Roles.Valet;
    public RegistrationApprovalStatus Status { get; set; } = RegistrationApprovalStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

public sealed class CompanyInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Valet;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; } = 1;
    public int UsedCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
}

public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool Success { get; set; } = true;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
