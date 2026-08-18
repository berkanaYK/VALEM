using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Contracts;

namespace VALE.Api.Services;

public sealed class TicketService(ValeDbContext db, CurrentUserContext currentUser, IFeeCalculator feeCalculator, IOptions<BusinessRulesOptions> businessRules, AuditService audit)
{
    private const int MaxPhotoBytes = 4_000_000;
    private readonly BusinessRulesOptions _rules = businessRules.Value;

    public async Task<PagedResponse<TicketSummaryDto>> QueryAsync(Guid? requestedBranchId, string? search, TicketStatus? status, bool includeClosed, int page, int pageSize, CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(requestedBranchId);
        await EnsureTenantBranchAsync(branchId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ParkingTickets.AsNoTracking().Where(x => x.BranchId == branchId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        else if (!includeClosed) query = query.Where(x => x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedPlate = TextNormalizer.Plate(search);
            var normalizedPhone = TextNormalizer.Phone(search);
            var ticketSearch = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Vehicle.NormalizedPlate.Contains(normalizedPlate) || x.TicketNumber.Contains(ticketSearch) || (normalizedPhone != null && x.Customer != null && x.Customer.NormalizedPhone != null && x.Customer.NormalizedPhone.Contains(normalizedPhone)));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var tickets = await query.Include(x => x.Branch).Include(x => x.Vehicle).Include(x => x.Customer).OrderByDescending(x => x.EntryAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResponse<TicketSummaryDto>(tickets.Select(Map).ToList(), page, pageSize, totalCount);
    }

    public async Task<TicketDetailDto> GetDetailAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        return new TicketDetailDto(Map(ticket), ticket.Vehicle.PhotoBase64);
    }

    public async Task<TicketSummaryDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(request.BranchId);
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == branchId && x.CompanyId == currentUser.CompanyId && x.IsActive, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Seçilen şube firmanıza ait değil, aktif değil veya bulunamadı.");
        var normalizedPlate = TextNormalizer.Plate(request.LicensePlate);
        if (normalizedPlate.Length < 3) throw new ApiException(StatusCodes.Status400BadRequest, "Plaka geçersiz", "Geçerli bir araç plakası girin.");
        var hasActiveTicket = await db.ParkingTickets.AnyAsync(x => x.BranchId == branchId && x.Vehicle.NormalizedPlate == normalizedPlate && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
        if (hasActiveTicket) throw new ApiException(StatusCodes.Status409Conflict, "Araç zaten içeride", "Bu plakaya ait açık bir vale kaydı bulunuyor.");

        var vehicle = await UpsertVehicleAsync(normalizedPlate, request.LicensePlate, request.Brand, request.Model, request.Color, request.Year, request.FuelType, request.Transmission, request.PhotoBase64, false, cancellationToken);
        var customer = await UpsertCustomerAsync(request.CustomerName, request.CustomerPhone, cancellationToken);
        var ticket = new ParkingTicket
        {
            TicketNumber = GenerateTicketNumber(), BranchId = branchId, Branch = branch, Vehicle = vehicle, Customer = customer,
            AssignedUserId = currentUser.UserId, CreatedByUserId = currentUser.UserId,
            KeyTag = Clean(request.KeyTag), ParkingSpot = Clean(request.ParkingSpot), Notes = Clean(request.Notes),
            HourlyRate = request.HourlyRate ?? _rules.DefaultHourlyRate, Status = TicketStatus.Received, EntryAt = DateTimeOffset.UtcNow
        };
        db.ParkingTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, branchId, "ticket.created", "ParkingTicket", ticket.Id.ToString(), $"{vehicle.LicensePlate} aracı kabul edildi. Fiş: {ticket.TicketNumber}", cancellationToken: cancellationToken);
        return Map(ticket);
    }

    public async Task<TicketSummaryDto> UpdateDetailsAsync(Guid ticketId, UpdateTicketDetailsRequest request, CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        var normalizedPlate = TextNormalizer.Plate(request.LicensePlate);
        if (normalizedPlate.Length < 3) throw new ApiException(StatusCodes.Status400BadRequest, "Plaka geçersiz", "Geçerli bir araç plakası girin.");
        if (normalizedPlate != ticket.Vehicle.NormalizedPlate)
        {
            var duplicate = await db.ParkingTickets.AnyAsync(x => x.Id != ticket.Id && x.BranchId == ticket.BranchId && x.Vehicle.NormalizedPlate == normalizedPlate && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
            if (duplicate) throw new ApiException(StatusCodes.Status409Conflict, "Plaka kullanımda", "Bu plakaya ait başka bir açık vale kaydı var.");
            ticket.Vehicle.LicensePlate = request.LicensePlate.Trim().ToUpperInvariant();
            ticket.Vehicle.NormalizedPlate = normalizedPlate;
        }
        ticket.Vehicle.Brand = Clean(request.Brand);
        ticket.Vehicle.Model = Clean(request.Model);
        ticket.Vehicle.Color = Clean(request.Color);
        ticket.Vehicle.Year = request.Year;
        ticket.Vehicle.FuelType = Clean(request.FuelType);
        ticket.Vehicle.Transmission = Clean(request.Transmission);
        if (request.RemovePhoto) ticket.Vehicle.PhotoBase64 = null;
        else if (!string.IsNullOrWhiteSpace(request.PhotoBase64)) ticket.Vehicle.PhotoBase64 = NormalizePhoto(request.PhotoBase64);
        ticket.Customer = await UpsertCustomerAsync(request.CustomerName, request.CustomerPhone, cancellationToken);
        ticket.KeyTag = Clean(request.KeyTag);
        ticket.ParkingSpot = Clean(request.ParkingSpot);
        ticket.Notes = Clean(request.Notes);
        if (ticket.Status != TicketStatus.Delivered && request.HourlyRate.HasValue) ticket.HourlyRate = request.HourlyRate.Value;
        ticket.UpdatedByUserId = currentUser.UserId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, ticket.BranchId, "ticket.updated", "ParkingTicket", ticket.Id.ToString(), $"{ticket.Vehicle.LicensePlate} kaydı düzeltildi.", cancellationToken: cancellationToken);
        return Map(ticket);
    }

    public async Task DeleteAsync(Guid ticketId, string reason, CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        if (ticket.Status == TicketStatus.Delivered || ticket.Payments.Count > 0 || ticket.PaidAmount > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "Kayıt silinemez", "Ödeme veya teslim kaydı bulunan işlemler mali bütünlük nedeniyle silinemez. Gerekirse not/düzeltme kullanın.");
        ticket.DeletedAt = DateTimeOffset.UtcNow;
        ticket.DeletedByUserId = currentUser.UserId;
        ticket.DeletedReason = reason.Trim();
        ticket.UpdatedAt = ticket.DeletedAt;
        ticket.UpdatedByUserId = currentUser.UserId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, ticket.BranchId, "ticket.deleted", "ParkingTicket", ticket.Id.ToString(), $"{ticket.Vehicle.LicensePlate} kaydı kaldırıldı. Neden: {reason.Trim()}", cancellationToken: cancellationToken);
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid ticketId, TicketStatus nextStatus, CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        if (!CanTransition(ticket.Status, nextStatus)) throw new ApiException(StatusCodes.Status409Conflict, "Durum değiştirilemedi", $"{ticket.Status} durumundan {nextStatus} durumuna doğrudan geçilemez.");
        ticket.Status = nextStatus;
        if (nextStatus == TicketStatus.Requested) ticket.RequestedAt = DateTimeOffset.UtcNow;
        else if (nextStatus == TicketStatus.Parked) ticket.RequestedAt = null;
        else if (nextStatus == TicketStatus.Cancelled) ticket.ExitAt = DateTimeOffset.UtcNow;
        ticket.UpdatedByUserId = currentUser.UserId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (nextStatus == TicketStatus.Requested)
            await AddBranchNotificationsAsync(ticket.BranchId, "Araç teslim istendi", $"{ticket.Vehicle.LicensePlate} plakalı araç teslim için isteniyor.", "VehicleRequested", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, ticket.BranchId, "ticket.status", "ParkingTicket", ticket.Id.ToString(), $"Durum: {nextStatus}", cancellationToken: cancellationToken);
        return Map(ticket);
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid ticketId, PaymentMethod paymentMethod, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(paymentMethod)) throw new ApiException(StatusCodes.Status400BadRequest, "Ödeme yöntemi geçersiz", "Nakit, kart veya havale/EFT yöntemlerinden birini seçin.");
        var ticket = await GetTicketAsync(ticketId, cancellationToken);
        if (ticket.Status is TicketStatus.Delivered or TicketStatus.Cancelled) throw new ApiException(StatusCodes.Status409Conflict, "Kayıt kapalı", "Bu araç daha önce teslim edilmiş veya kayıt iptal edilmiş.");
        var exitAt = DateTimeOffset.UtcNow;
        var amount = feeCalculator.Calculate(ticket.EntryAt, exitAt, ticket.HourlyRate);
        ticket.AmountDue = amount; ticket.PaidAmount = amount; ticket.ExitAt = exitAt; ticket.Status = TicketStatus.Delivered;
        ticket.UpdatedByUserId = currentUser.UserId; ticket.UpdatedAt = exitAt;
        ticket.Payments.Add(new Payment { Amount = amount, Method = paymentMethod, PaidAt = exitAt, RecordedByUserId = currentUser.UserId });
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(currentUser.UserId, ticket.BranchId, "ticket.checkout", "ParkingTicket", ticket.Id.ToString(), $"Teslim tamamlandı. {amount:N2} TL, yöntem: {paymentMethod}", cancellationToken: cancellationToken);
        return new CheckoutResponse(Map(ticket), amount);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid? requestedBranchId, CancellationToken cancellationToken)
    {
        var branchId = currentUser.ResolveBranchId(requestedBranchId);
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId && x.CompanyId == currentUser.CompanyId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Şube bulunamadı", "Seçilen şube firmanıza ait değil veya bulunamadı.");
        var localDayStartUtc = GetLocalDayStartUtc();
        var activeVehicles = await db.ParkingTickets.CountAsync(x => x.BranchId == branchId && x.Status != TicketStatus.Delivered && x.Status != TicketStatus.Cancelled, cancellationToken);
        var waiting = await db.ParkingTickets.CountAsync(x => x.BranchId == branchId && x.Status == TicketStatus.Requested, cancellationToken);
        var deliveredToday = await db.ParkingTickets.CountAsync(x => x.BranchId == branchId && x.Status == TicketStatus.Delivered && x.ExitAt >= localDayStartUtc, cancellationToken);
        var revenueToday = currentUser.CanViewFinancials
            ? await db.Payments.Where(x => x.Ticket.BranchId == branchId && x.PaidAt >= localDayStartUtc).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m
            : 0m;
        var recent = await db.ParkingTickets.AsNoTracking().Include(x => x.Branch).Include(x => x.Vehicle).Include(x => x.Customer).Where(x => x.BranchId == branchId).OrderByDescending(x => x.EntryAt).Take(8).ToListAsync(cancellationToken);
        return new DashboardDto(branch.Id, branch.Name, activeVehicles, waiting, deliveredToday, revenueToday, recent.Select(Map).ToList());
    }

    private async Task<ParkingTicket> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.ParkingTickets.Include(x => x.Branch).Include(x => x.Vehicle).Include(x => x.Customer).Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Kayıt bulunamadı", "Vale kaydı bulunamadı.");
        if (ticket.Branch.CompanyId != currentUser.CompanyId)
            throw new ApiException(StatusCodes.Status403Forbidden, "Firma yetkisi yok", "Bu kayıt başka bir firmaya ait.");
        currentUser.EnsureBranchAccess(ticket.BranchId);
        return ticket;
    }

    private async Task EnsureTenantBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        if (!await db.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId && x.CompanyId == currentUser.CompanyId && x.IsActive, cancellationToken))
            throw new ApiException(StatusCodes.Status403Forbidden, "Şube yetkisi yok", "Bu şube firmanıza ait değil veya aktif değil.");
    }

    private async Task AddBranchNotificationsAsync(Guid branchId, string title, string body, string type, CancellationToken cancellationToken)
    {
        var recipients = await db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == currentUser.CompanyId && x.BranchId == branchId)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var userId in recipients)
            db.Notifications.Add(new ValeNotification { CompanyId = currentUser.CompanyId, BranchId = branchId, UserId = userId, Title = title, Body = body, Type = type });
    }

    private async Task<Vehicle> UpsertVehicleAsync(string normalizedPlate, string plate, string? brand, string? model, string? color, int? year, string? fuel, string? transmission, string? photoBase64, bool removePhoto, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.NormalizedPlate == normalizedPlate, cancellationToken);
        var photo = string.IsNullOrWhiteSpace(photoBase64) ? null : NormalizePhoto(photoBase64);
        if (vehicle is null)
        {
            vehicle = new Vehicle { LicensePlate = plate.Trim().ToUpperInvariant(), NormalizedPlate = normalizedPlate, Brand = Clean(brand), Model = Clean(model), Color = Clean(color), Year = year, FuelType = Clean(fuel), Transmission = Clean(transmission), PhotoBase64 = photo };
            db.Vehicles.Add(vehicle);
        }
        else
        {
            vehicle.Brand = Clean(brand) ?? vehicle.Brand; vehicle.Model = Clean(model) ?? vehicle.Model; vehicle.Color = Clean(color) ?? vehicle.Color;
            vehicle.Year = year ?? vehicle.Year; vehicle.FuelType = Clean(fuel) ?? vehicle.FuelType; vehicle.Transmission = Clean(transmission) ?? vehicle.Transmission;
            if (removePhoto) vehicle.PhotoBase64 = null; else if (photo is not null) vehicle.PhotoBase64 = photo;
        }
        return vehicle;
    }

    private async Task<Customer?> UpsertCustomerAsync(string? name, string? phone, CancellationToken cancellationToken)
    {
        var normalizedPhone = TextNormalizer.Phone(phone);
        Customer? customer = null;
        if (normalizedPhone is not null) customer = await db.Customers.SingleOrDefaultAsync(x => x.NormalizedPhone == normalizedPhone, cancellationToken);
        if (customer is null && (!string.IsNullOrWhiteSpace(name) || normalizedPhone is not null))
        {
            customer = new Customer { Name = Clean(name) ?? "Misafir Müşteri", Phone = Clean(phone), NormalizedPhone = normalizedPhone };
            db.Customers.Add(customer);
        }
        else if (customer is not null)
        {
            if (!string.IsNullOrWhiteSpace(name)) customer.Name = name.Trim();
            if (!string.IsNullOrWhiteSpace(phone)) customer.Phone = phone.Trim();
        }
        return customer;
    }

    private static bool CanTransition(TicketStatus current, TicketStatus next) => (current, next) switch
    {
        (TicketStatus.Received, TicketStatus.Parked) => true, (TicketStatus.Received, TicketStatus.Requested) => true, (TicketStatus.Received, TicketStatus.Cancelled) => true,
        (TicketStatus.Parked, TicketStatus.Requested) => true, (TicketStatus.Parked, TicketStatus.Cancelled) => true,
        (TicketStatus.Requested, TicketStatus.Parked) => true, (TicketStatus.Requested, TicketStatus.Cancelled) => true, _ => false
    };

    private TicketSummaryDto Map(ParkingTicket ticket)
    {
        var vehicleDescription = string.Join(" ", new[] { ticket.Vehicle.Brand, ticket.Vehicle.Model, ticket.Vehicle.Color }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var liveAmount = ticket.Status is TicketStatus.Delivered or TicketStatus.Cancelled
            ? ticket.AmountDue
            : feeCalculator.Calculate(ticket.EntryAt, DateTimeOffset.UtcNow, ticket.HourlyRate);
        return new TicketSummaryDto(ticket.Id, ticket.TicketNumber, ticket.BranchId, ticket.Branch.Name, ticket.Vehicle.LicensePlate,
            string.IsNullOrWhiteSpace(vehicleDescription) ? "Araç bilgisi yok" : vehicleDescription, ticket.Customer?.Name, ticket.Customer?.Phone,
            ticket.KeyTag, ticket.ParkingSpot, ticket.Status, ticket.EntryAt, ticket.RequestedAt, ticket.ExitAt, ticket.HourlyRate,
            liveAmount, ticket.PaidAmount, ticket.Notes, ticket.Vehicle.Year, ticket.Vehicle.FuelType, ticket.Vehicle.Transmission,
            !string.IsNullOrWhiteSpace(ticket.Vehicle.PhotoBase64));
    }

    private DateTimeOffset GetLocalDayStartUtc()
    {
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(_rules.TimeZoneId); }
        catch (TimeZoneNotFoundException) { timeZone = TimeZoneInfo.Utc; }
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return new DateTimeOffset(localNow.Date, localNow.Offset).ToUniversalTime();
    }

    private static string GenerateTicketNumber()
    {
        Span<byte> randomBytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        return $"VALE-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Convert.ToHexString(randomBytes)[..6]}";
    }

    private static string? NormalizePhoto(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        var value = base64.Trim();
        var comma = value.IndexOf(',');
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && comma >= 0) value = value[(comma + 1)..];
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { throw new ApiException(StatusCodes.Status400BadRequest, "Fotoğraf geçersiz", "Araç fotoğrafı geçerli bir görsel verisi değil."); }
        if (bytes.Length > MaxPhotoBytes) throw new ApiException(StatusCodes.Status413PayloadTooLarge, "Fotoğraf çok büyük", "Araç fotoğrafı en fazla 4 MB olabilir.");
        return value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
