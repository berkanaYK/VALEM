using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record CreateTicketRequest(
    Guid? BranchId,
    [param: Required, MinLength(3), MaxLength(16)] string LicensePlate,
    [param: MaxLength(60)] string? Brand,
    [param: MaxLength(60)] string? Model,
    [param: MaxLength(40)] string? Color,
    [param: MaxLength(120)] string? CustomerName,
    [param: MaxLength(30)] string? CustomerPhone,
    [param: MaxLength(30)] string? KeyTag,
    [param: MaxLength(30)] string? ParkingSpot,
    [param: MaxLength(500)] string? Notes,
    [param: Range(0.01, 100000)] decimal? HourlyRate);

public sealed record UpdateTicketStatusRequest(TicketStatus Status);

public sealed record CheckoutTicketRequest(PaymentMethod PaymentMethod);

public sealed record CheckoutResponse(
    TicketSummaryDto Ticket,
    decimal PaidAmount);

public sealed record TicketSummaryDto(
    Guid Id,
    string TicketNumber,
    Guid BranchId,
    string BranchName,
    string LicensePlate,
    string VehicleDescription,
    string? CustomerName,
    string? CustomerPhone,
    string? KeyTag,
    string? ParkingSpot,
    TicketStatus Status,
    DateTimeOffset EntryAt,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? ExitAt,
    decimal HourlyRate,
    decimal AmountDue,
    decimal PaidAmount,
    string? Notes);
