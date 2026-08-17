namespace VALE.Contracts;

public sealed record DashboardDto(
    Guid BranchId,
    string BranchName,
    int ActiveVehicles,
    int WaitingForDelivery,
    int DeliveredToday,
    decimal RevenueToday,
    IReadOnlyList<TicketSummaryDto> RecentTickets);

