namespace VALE.Contracts;

public sealed record ReportSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalVehicles,
    int DeliveredVehicles,
    int CancelledVehicles,
    int ActiveVehicles,
    decimal Revenue,
    decimal AverageRevenuePerDelivery,
    double AverageParkingMinutes,
    IReadOnlyList<PaymentBreakdownDto> Payments,
    IReadOnlyList<DailyReportPointDto> Daily);

public sealed record PaymentBreakdownDto(
    PaymentMethod Method,
    int Count,
    decimal Amount);

public sealed record DailyReportPointDto(
    DateOnly Day,
    int Vehicles,
    int Delivered,
    decimal Revenue);
