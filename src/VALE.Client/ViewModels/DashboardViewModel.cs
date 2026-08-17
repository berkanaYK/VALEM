using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed record MetricCardViewModel(string Title, string Value, string Description);

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;
    private readonly BranchContext _branchContext;
    private string _branchName = "Şube";

    public DashboardViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<MetricCardViewModel> Metrics { get; } = [];
    public ObservableCollection<TicketSummaryDto> RecentTickets { get; } = [];

    public string BranchName
    {
        get => _branchName;
        private set => SetProperty(ref _branchName, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public Task LoadAsync() => ExecuteAsync(async () =>
    {
        var dashboard = await _api.GetDashboardAsync(_branchContext.ActiveBranchId ?? _session.User?.BranchId);
        BranchName = dashboard.BranchName;

        Metrics.Clear();
        Metrics.Add(new MetricCardViewModel("Aktif araç", dashboard.ActiveVehicles.ToString("N0"), "Şu anda içeride"));
        Metrics.Add(new MetricCardViewModel("Teslim bekleyen", dashboard.WaitingForDelivery.ToString("N0"), "Araç istenen kayıtlar"));
        Metrics.Add(new MetricCardViewModel("Bugün teslim", dashboard.DeliveredToday.ToString("N0"), "Tamamlanan işlemler"));
        Metrics.Add(new MetricCardViewModel("Günlük ciro", $"{dashboard.RevenueToday:N2} TL", "Bugünkü tahsilat"));

        RecentTickets.Clear();
        foreach (var ticket in dashboard.RecentTickets)
        {
            RecentTickets.Add(ticket);
        }
    });
}
