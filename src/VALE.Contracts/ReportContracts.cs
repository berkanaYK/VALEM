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
    IReadOnlyList<DailyReportPointDto> Daily,
    IReadOnlyList<HourlyReportPointDto> Hourly,
    IReadOnlyList<VehicleBrandReportDto> TopBrands);

public sealed record PaymentBreakdownDto(
    PaymentMethod Method,
    int Count,
    decimal Amount);

public sealed record DailyReportPointDto(
    DateOnly Day,
    int Vehicles,
    int Delivered,
    decimal Revenue);

public sealed record HourlyReportPointDto(
    int Hour,
    int Delivered,
    decimal Revenue);

public sealed record VehicleBrandReportDto(
    string Brand,
    int Vehicles);
