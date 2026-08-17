using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;
    private readonly BranchContext _branchContext;
    private string _searchText = string.Empty;

    public HistoryViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<TicketSummaryDto> Tickets { get; } = [];
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public IAsyncRelayCommand RefreshCommand { get; }

    public Task LoadAsync() => ExecuteAsync(async () =>
    {
        var result = await _api.GetTicketsAsync(_branchContext.ActiveBranchId ?? _session.User?.BranchId, SearchText, true);
        Tickets.Clear();
        foreach (var ticket in result.Items)
        {
            Tickets.Add(ticket);
        }
    });
}
