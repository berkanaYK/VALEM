using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly BranchContext _branchContext;
    private DateTimeOffset? _startDate;
    private DateTimeOffset? _endDate;
    private string _totalVehicles = "—";
    private string _deliveredVehicles = "—";
    private string _revenue = "—";
    private string _averageRevenue = "—";
    private string _averageParking = "—";
    private string _cancelledVehicles = "—";
    private string _paymentBreakdown = "—";
    private string _topBrands = "—";

    public ReportsViewModel(ApiClient api, BranchContext branchContext)
    {
        _api = api;
        _branchContext = branchContext;
        var today = DateTimeOffset.Now.Date;
        StartDate = new DateTimeOffset(today.AddDays(-6), TimeZoneInfo.Local.GetUtcOffset(today));
        EndDate = new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public DateTimeOffset? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTimeOffset? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public string TotalVehicles { get => _totalVehicles; private set => SetProperty(ref _totalVehicles, value); }
    public string DeliveredVehicles { get => _deliveredVehicles; private set => SetProperty(ref _deliveredVehicles, value); }
    public string Revenue { get => _revenue; private set => SetProperty(ref _revenue, value); }
    public string AverageRevenue { get => _averageRevenue; private set => SetProperty(ref _averageRevenue, value); }
    public string AverageParking { get => _averageParking; private set => SetProperty(ref _averageParking, value); }
    public string CancelledVehicles { get => _cancelledVehicles; private set => SetProperty(ref _cancelledVehicles, value); }
    public string PaymentBreakdown { get => _paymentBreakdown; private set => SetProperty(ref _paymentBreakdown, value); }
    public string TopBrands { get => _topBrands; private set => SetProperty(ref _topBrands, value); }

    public ObservableCollection<DailyReportRow> Daily { get; } = [];
    public ObservableCollection<HourlyReportRow> Hourly { get; } = [];
    public IAsyncRelayCommand LoadCommand { get; }

    public void ApplyPreset(int index)
    {
        var today = DateTime.Today;
        var offset = TimeZoneInfo.Local.GetUtcOffset(today);
        switch (index)
        {
            case 0:
                StartDate = new DateTimeOffset(today, offset);
                EndDate = new DateTimeOffset(today, offset);
                break;
            case 1:
                StartDate = new DateTimeOffset(today.AddDays(-6), offset);
                EndDate = new DateTimeOffset(today, offset);
                break;
            case 2:
                StartDate = new DateTimeOffset(today.AddDays(-29), offset);
                EndDate = new DateTimeOffset(today, offset);
                break;
            case 3:
                StartDate = new DateTimeOffset(today.AddDays(-89), offset);
                EndDate = new DateTimeOffset(today, offset);
                break;
        }
    }

    private Task LoadAsync() => ExecuteAsync(async () =>
    {
        var start = StartDate ?? throw new InvalidOperationException("Başlangıç tarihi seçin.");
        var end = EndDate ?? throw new InvalidOperationException("Bitiş tarihi seçin.");
        if (end.Date < start.Date)
        {
            throw new InvalidOperationException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }

        var from = ToLocalStart(start.Date);
        var to = ToLocalStart(end.Date.AddDays(1)).AddTicks(-1);
        var report = await _api.GetReportAsync(_branchContext.ActiveBranchId, from, to);

        TotalVehicles = report.TotalVehicles.ToString(CultureInfo.CurrentCulture);
        DeliveredVehicles = report.DeliveredVehicles.ToString(CultureInfo.CurrentCulture);
        Revenue = $"{report.Revenue:N2} TL";
        AverageRevenue = $"{report.AverageRevenuePerDelivery:N2} TL";
        AverageParking = FormatDuration(report.AverageParkingMinutes);
        CancelledVehicles = report.CancelledVehicles.ToString(CultureInfo.CurrentCulture);
        PaymentBreakdown = report.Payments.Count == 0
            ? "Bu dönemde tahsilat yok."
            : string.Join(Environment.NewLine, report.Payments.Select(x =>
                $"{PaymentText(x.Method)}  •  {x.Count} işlem  •  {x.Amount:N2} TL"));
        TopBrands = report.TopBrands.Count == 0
            ? "Bu dönem için araç markası verisi yok."
            : string.Join(Environment.NewLine, report.TopBrands.Select((x, index) =>
                $"{index + 1}. {x.Brand}  •  {x.Vehicles} araç"));

        Daily.Clear();
        var maxRevenue = Math.Max(1m, report.Daily.Select(x => x.Revenue).DefaultIfEmpty(0m).Max());
        foreach (var point in report.Daily)
        {
            Daily.Add(new DailyReportRow(
                point.Day.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
                $"{point.Vehicles} giriş  •  {point.Delivered} teslim",
                $"{point.Revenue:N2} TL",
                (double)point.Revenue,
                (double)maxRevenue));
        }

        Hourly.Clear();
        var maxDelivered = Math.Max(1, report.Hourly.Select(x => x.Delivered).DefaultIfEmpty(0).Max());
        foreach (var point in report.Hourly.Where(x => x.Delivered > 0 || x.Revenue > 0))
        {
            Hourly.Add(new HourlyReportRow(
                $"{point.Hour:00}:00",
                $"{point.Delivered} teslim  •  {point.Revenue:N2} TL",
                point.Delivered,
                maxDelivered));
        }
    });

    private static DateTimeOffset ToLocalStart(DateTime date)
    {
        var unspecified = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    private static string FormatDuration(double minutes) =>
        minutes < 60 ? $"{minutes:N0} dk" : $"{minutes / 60d:N1} sa";

    private static string PaymentText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Card => "Kart",
        _ => "Havale / EFT"
    };
}

public sealed record DailyReportRow(
    string Day,
    string Summary,
    string RevenueText,
    double Revenue,
    double MaximumRevenue);

public sealed record HourlyReportRow(
    string Hour,
    string Summary,
    double Delivered,
    double MaximumDelivered);
