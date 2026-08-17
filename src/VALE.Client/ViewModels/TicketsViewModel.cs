using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class TicketsViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;
    private readonly BranchContext _branchContext;
    private string _searchText = string.Empty;
    private TicketSummaryDto? _selectedTicket;

    public TicketsViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<TicketSummaryDto> Tickets { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public TicketSummaryDto? SelectedTicket
    {
        get => _selectedTicket;
        set
        {
            if (SetProperty(ref _selectedTicket, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanUpdateStatus));
                OnPropertyChanged(nameof(CanCheckout));
            }
        }
    }

    public bool HasSelection => SelectedTicket is not null;
    public bool CanUpdateStatus => HasSelection && HasAnyRole("Admin", "Manager", "Valet");
    public bool CanCheckout => HasSelection && HasAnyRole("Admin", "Manager", "Cashier");
    public IAsyncRelayCommand RefreshCommand { get; }

    public Task LoadAsync() => ExecuteAsync(LoadCoreAsync);

    public Task UpdateStatusAsync(TicketStatus status) => ExecuteAsync(async () =>
    {
        var ticket = SelectedTicket ?? throw new InvalidOperationException("Önce bir araç kaydı seçin.");
        await _api.UpdateTicketStatusAsync(ticket.Id, status);
        await LoadCoreAsync();
    });

    public Task CheckoutAsync(PaymentMethod method) => ExecuteAsync(async () =>
    {
        var ticket = SelectedTicket ?? throw new InvalidOperationException("Önce teslim edilecek araç kaydını seçin.");
        await _api.CheckoutAsync(ticket.Id, method);
        await LoadCoreAsync();
    });

    private async Task LoadCoreAsync()
    {
        var result = await _api.GetTicketsAsync(_branchContext.ActiveBranchId ?? _session.User?.BranchId, SearchText, false);
        Tickets.Clear();
        foreach (var ticket in result.Items)
        {
            Tickets.Add(ticket);
        }

        SelectedTicket = null;
    }

    private bool HasAnyRole(params string[] roles) =>
        _session.User?.Roles.Any(currentRole =>
            roles.Contains(currentRole, StringComparer.OrdinalIgnoreCase)) == true;
}
