using System.ComponentModel.DataAnnotations;
using VALE.Contracts;

namespace VALE.Api.Areas.PlatformAdmin.Models;

public sealed class PlatformLoginModel
{
    [Required, EmailAddress, MaxLength(256)]
    [Display(Name = "Geliştirici e-postası")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MaxLength(128)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed record PlatformDashboardModel(
    int Companies,
    int ActiveCompanies,
    int Users,
    int ActiveUsers,
    int Branches,
    int ActiveTickets,
    int PendingRegistrations,
    decimal PaymentsLast24Hours,
    IReadOnlyList<PlatformAuditRow> RecentActions);

public sealed record PlatformAuditRow(
    DateTimeOffset OccurredAt,
    string AdminName,
    string Action,
    string EntityType,
    string? EntityId,
    string Detail,
    string? IpAddress);

public sealed record PlatformCompanyRow(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    int Branches,
    int Users,
    int ActiveTickets,
    DateTimeOffset CreatedAt);

public sealed class PlatformCompanyEditModel
{
    public Guid Id { get; set; }

    [Required, MinLength(2), MaxLength(160)]
    [Display(Name = "Firma adı")]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(40), RegularExpression("^[A-Za-z0-9_-]+$")]
    [Display(Name = "Firma kodu")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Firma aktif")]
    public bool IsActive { get; set; }

    [Display(Name = "Ana firma sahibi")]
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public List<Guid> OwnerCandidateIds { get; set; } = [];
    public List<string> OwnerCandidateNames { get; set; } = [];
    public int BranchCount { get; set; }
    public int UserCount { get; set; }
    public int ActiveTicketCount { get; set; }
}

public sealed record PlatformUserRow(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string FullName,
    string Email,
    string? BranchName,
    bool EmailConfirmed,
    bool IsActive,
    bool IsLocked,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed class PlatformUserEditModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(120)]
    [Display(Name = "Ad soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [MaxLength(80)]
    [Display(Name = "Görev")]
    public string? JobTitle { get; set; }

    [Display(Name = "Şube")]
    public Guid BranchId { get; set; }

    [Display(Name = "Hesap aktif")]
    public bool IsActive { get; set; }

    [Display(Name = "E-posta doğrulandı")]
    public bool EmailConfirmed { get; set; }

    public List<Guid> BranchIds { get; set; } = [];
    public List<string> BranchNames { get; set; } = [];
    public List<string> SelectedRoles { get; set; } = [];
    public List<string> AvailableRoles { get; set; } = [];
    public bool IsLocked { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public int ActiveDeviceSessions { get; set; }
    public int ActivePushRegistrations { get; set; }
}

public sealed record PlatformRegistrationRow(
    Guid Id,
    Guid ApplicantUserId,
    string ApplicantName,
    string ApplicantEmail,
    bool EmailConfirmed,
    Guid CompanyId,
    string CompanyName,
    string BranchName,
    string RequestedRole,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? Note);

public sealed record PlatformBranchRow(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Code,
    string Name,
    string City,
    string? InviteCode,
    bool IsActive,
    int ActiveTickets);

public sealed class PlatformBranchEditModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(20)]
    [Display(Name = "Şube kodu")]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(120)]
    [Display(Name = "Şube adı")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    [Display(Name = "Şehir")]
    public string City { get; set; } = string.Empty;

    [MaxLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [Display(Name = "Şube aktif")]
    public bool IsActive { get; set; }

    public string? InviteCode { get; set; }
    public int UserCount { get; set; }
    public int ActiveTicketCount { get; set; }
}

public sealed record PlatformTicketRow(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string BranchName,
    string TicketNumber,
    string LicensePlate,
    TicketStatus Status,
    DateTimeOffset EntryAt,
    decimal AmountDue,
    decimal PaidAmount,
    bool IsDeleted);

public sealed class PlatformTicketEditModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;

    [Required, MinLength(3), MaxLength(16)]
    [Display(Name = "Plaka")]
    public string LicensePlate { get; set; } = string.Empty;

    [MaxLength(60)] public string? Brand { get; set; }
    [MaxLength(60)] public string? Model { get; set; }
    [MaxLength(40)] public string? Color { get; set; }
    [MaxLength(30)] public string? KeyTag { get; set; }
    [MaxLength(30)] public string? ParkingSpot { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public TicketStatus Status { get; set; }
    [Range(0, 1000000)] public decimal HourlyRate { get; set; }
    [Range(0, 1000000)] public decimal AmountDue { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTimeOffset EntryAt { get; set; }
    public DateTimeOffset? ExitAt { get; set; }
    public bool IsDeleted { get; set; }
    [MaxLength(300)] public string? DeletedReason { get; set; }
}

public sealed record TenantAuditRow(
    DateTimeOffset OccurredAt,
    string CompanyName,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string Detail,
    bool Success,
    string? IpAddress);

public sealed record PlatformAuditIndexModel(
    IReadOnlyList<PlatformAuditRow> PlatformActions,
    IReadOnlyList<TenantAuditRow> TenantActions);

public sealed record PlatformSystemModel(
    bool DatabaseConnected,
    string DatabaseProvider,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations,
    bool EmailConfigured,
    bool FirebaseConfigured,
    int ActiveDeviceSessions,
    int ActivePushRegistrations,
    int UnreadNotifications,
    int TenantAuditEntries,
    int PlatformAuditEntries,
    DateTimeOffset CheckedAt);
