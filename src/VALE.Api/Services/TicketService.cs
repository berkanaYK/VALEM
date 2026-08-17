using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Contracts;

namespace VALE.Api.Services;

public sealed class TicketService(
    ValeDbContext db,
    CurrentUserContext currentUser,
    IFeeCalculator feeCalculator,
    IOptions<BusinessRulesOptions> businessRules)
{
    private readonly BusinessRulesOptions _rules = businessRules.Value;

    public async Task<PagedResponse<TicketSummaryDto>> QueryAsync(
        Guid? requestedBranchId,
        string? search,
        TicketStatus? status,
        bool includeClosed,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(requestedBranchId);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ParkingTickets
            .AsNoTracking()
            .Where(x => x.BranchId == branchId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }
        else if (!includeClosed)
        {
            query = query.Where(x => x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedPlate = TextNormalizer.Plate(search);
            var normalizedPhone = TextNormalizer.Phone(search);
            var ticketSearch = search.Trim().ToUpperInvariant();

            query = query.Where(x =>
                x.Vehicle.NormalizedPlate.Contains(normalizedPlate) ||
                x.TicketNumber.Contains(ticketSearch) ||
                (normalizedPhone != null && x.Customer != null && x.Customer.NormalizedPhone != null &&
                 x.Customer.NormalizedPhone.Contains(normalizedPhone)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var tickets = await query
            .Include(x => x.Branch)
            .Include(x => x.Vehicle)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.EntryAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<TicketSummaryDto>(
            tickets.Select(Map).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<TicketSummaryDto> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(request.BranchId);
        var branch = await db.Branches.SingleOrDefaultAsync(
            x => x.Id == branchId && x.IsActive,
            cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Seçilen şube aktif değil veya bulunamadı.");

        var normalizedPlate = TextNormalizer.Plate(request.LicensePlate);
        if (normalizedPlate.Length < 3)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Plaka geçersiz", "Geçerli bir araç plakası girin.");
        }

        var hasActiveTicket = await db.ParkingTickets.AnyAsync(
            x => x.BranchId == branchId &&
                 x.Vehicle.NormalizedPlate == normalizedPlate &&
                 x.Status != TicketStatus.Delivered &&
                 x.Status != TicketStatus.Cancelled,
            cancellationToken);
        if (hasActiveTicket)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Araç zaten içeride", "Bu plakaya ait açık bir vale kaydı bulunuyor.");
        }

        var vehicle = await db.Vehicles.SingleOrDefaultAsync(
            x => x.NormalizedPlate == normalizedPlate,
            cancellationToken);
        if (vehicle is null)
        {
            vehicle = new Vehicle
            {
                LicensePlate = request.LicensePlate.Trim().ToUpperInvariant(),
                NormalizedPlate = normalizedPlate,
                Brand = Clean(request.Brand),
                Model = Clean(request.Model),
                Color = Clean(request.Color)
            };
            db.Vehicles.Add(vehicle);
        }
        else
        {
            vehicle.Brand = Clean(request.Brand) ?? vehicle.Brand;
            vehicle.Model = Clean(request.Model) ?? vehicle.Model;
            vehicle.Color = Clean(request.Color) ?? vehicle.Color;
        }

        Customer? customer = null;
        var normalizedPhone = TextNormalizer.Phone(request.CustomerPhone);
        if (normalizedPhone is not null)
        {
            customer = await db.Customers.SingleOrDefaultAsync(
                x => x.NormalizedPhone == normalizedPhone,
                cancellationToken);
        }

        if (customer is null && (!string.IsNullOrWhiteSpace(request.CustomerName) || normalizedPhone is not null))
        {
            customer = new Customer
            {
                Name = Clean(request.CustomerName) ?? "Misafir Müşteri",
                Phone = Clean(request.CustomerPhone),
                NormalizedPhone = normalizedPhone
            };
            db.Customers.Add(customer);
        }
        else if (customer is not null && !string.IsNullOrWhiteSpace(request.CustomerName))
        {
            customer.Name = request.CustomerName.Trim();
        }

        var hourlyRate = request.HourlyRate ?? _rules.DefaultHourlyRate;
        var ticket = new ParkingTicket
        {
            TicketNumber = GenerateTicketNumber(),
            BranchId = branchId,
            Branch = branch,
            Vehicle = vehicle,
            Customer = customer,
            AssignedUserId = currentUser.UserId,
            KeyTag = Clean(request.KeyTag),
            ParkingSpot = Clean(request.ParkingSpot),
            Notes = Clean(request.Notes),
            HourlyRate = hourlyRate,
            Status = TicketStatus.Received,
            EntryAt = DateTimeOffset.UtcNow
        };

        db.ParkingTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return Map(ticket);
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(
        Guid ticketId,
        TicketStatus nextStatus,
        CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        if (!CanTransition(ticket.Status, nextStatus))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "Durum değiştirilemedi",
                $"{ticket.Status} durumundan {nextStatus} durumuna doğrudan geçilemez.");
        }

        ticket.Status = nextStatus;
        if (nextStatus == TicketStatus.Requested)
        {
            ticket.RequestedAt = DateTimeOffset.UtcNow;
        }
        else if (nextStatus == TicketStatus.Parked)
        {
            ticket.RequestedAt = null;
        }
        else if (nextStatus == TicketStatus.Cancelled)
        {
            ticket.ExitAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(ticket);
    }

    public async Task<CheckoutResponse> CheckoutAsync(
        Guid ticketId,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(paymentMethod))
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "Ödeme yöntemi geçersiz",
                "Nakit, kart veya havale/EFT ödeme yöntemlerinden birini seçin.");
        }

        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        if (ticket.Status is TicketStatus.Delivered or TicketStatus.Cancelled)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Kayıt kapalı", "Bu araç daha önce teslim edilmiş veya kayıt iptal edilmiş.");
        }

        var exitAt = DateTimeOffset.UtcNow;
        var amount = feeCalculator.Calculate(ticket.EntryAt, exitAt, ticket.HourlyRate);
        ticket.AmountDue = amount;
        ticket.PaidAmount = amount;
        ticket.ExitAt = exitAt;
        ticket.Status = TicketStatus.Delivered;
        ticket.Payments.Add(new Payment
        {
            Amount = amount,
            Method = paymentMethod,
            PaidAt = exitAt,
            RecordedByUserId = currentUser.UserId
        });

        await db.SaveChangesAsync(cancellationToken);
        return new CheckoutResponse(Map(ticket), amount);
    }

    public async Task<DashboardDto> GetDashboardAsync(
        Guid? requestedBranchId,
        CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(requestedBranchId);
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Seçilen şube bulunamadı.");

        var localDayStartUtc = GetLocalDayStartUtc();
        var activeVehicles = await db.ParkingTickets.CountAsync(
            x => x.BranchId == branchId && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled,
            cancellationToken);
        var waiting = await db.ParkingTickets.CountAsync(
            x => x.BranchId == branchId && x.Status == TicketStatus.Requested,
            cancellationToken);
        var deliveredToday = await db.ParkingTickets.CountAsync(
            x => x.BranchId == branchId && x.Status == TicketStatus.Delivered && x.ExitAt >= localDayStartUtc,
            cancellationToken);
        var revenueToday = await db.Payments
            .Where(x => x.Ticket.BranchId == branchId && x.PaidAt >= localDayStartUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var recent = await db.ParkingTickets
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.Vehicle)
            .Include(x => x.Customer)
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.EntryAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        return new DashboardDto(
            branch.Id,
            branch.Name,
            activeVehicles,
            waiting,
            deliveredToday,
            revenueToday,
            recent.Select(Map).ToList());
    }

    private async Task<ParkingTicket> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets
            .Include(x => x.Branch)
            .Include(x => x.Vehicle)
            .Include(x => x.Customer)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Kayıt bulunamadı", "Vale kaydı bulunamadı.");

        currentUser.EnsureBranchAccess(ticket.BranchId);
        return ticket;
    }

    private static bool CanTransition(TicketStatus current, TicketStatus next) => (current, next) switch
    {
        (TicketStatus.Received, TicketStatus.Parked) => true,
        (TicketStatus.Received, TicketStatus.Requested) => true,
        (TicketStatus.Received, TicketStatus.Cancelled) => true,
        (TicketStatus.Parked, TicketStatus.Requested) => true,
        (TicketStatus.Parked, TicketStatus.Cancelled) => true,
        (TicketStatus.Requested, TicketStatus.Parked) => true,
        (TicketStatus.Requested, TicketStatus.Cancelled) => true,
        _ => false
    };

    private static TicketSummaryDto Map(ParkingTicket ticket)
    {
        var vehicleDescription = string.Join(
            " ",
            new[] { ticket.Vehicle.Brand, ticket.Vehicle.Model, ticket.Vehicle.Color }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new TicketSummaryDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.BranchId,
            ticket.Branch.Name,
            ticket.Vehicle.LicensePlate,
            string.IsNullOrWhiteSpace(vehicleDescription) ? "Araç bilgisi yok" : vehicleDescription,
            ticket.Customer?.Name,
            ticket.Customer?.Phone,
            ticket.KeyTag,
            ticket.ParkingSpot,
            ticket.Status,
            ticket.EntryAt,
            ticket.RequestedAt,
            ticket.ExitAt,
            ticket.HourlyRate,
            ticket.AmountDue,
            ticket.PaidAmount,
            ticket.Notes);
    }

    private DateTimeOffset GetLocalDayStartUtc()
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(_rules.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return new DateTimeOffset(localNow.Date, localNow.Offset).ToUniversalTime();
    }

    private static string GenerateTicketNumber()
    {
        Span<byte> randomBytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        return $"VALE-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Convert.ToHexString(randomBytes)[..6]}";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
