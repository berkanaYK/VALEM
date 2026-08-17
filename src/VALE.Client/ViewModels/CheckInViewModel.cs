using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class CheckInViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;
    private readonly BranchContext _branchContext;
    private string _licensePlate = string.Empty;
    private string _brand = string.Empty;
    private string _model = string.Empty;
    private string _color = string.Empty;
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _keyTag = string.Empty;
    private string _parkingSpot = string.Empty;
    private string _notes = string.Empty;
    private double _hourlyRate = 100d;
    private string? _successMessage;

    public CheckInViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
    }

    public string LicensePlate { get => _licensePlate; set => SetProperty(ref _licensePlate, value); }
    public string Brand { get => _brand; set => SetProperty(ref _brand, value); }
    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }
    public string KeyTag { get => _keyTag; set => SetProperty(ref _keyTag, value); }
    public string ParkingSpot { get => _parkingSpot; set => SetProperty(ref _parkingSpot, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public double HourlyRate { get => _hourlyRate; set => SetProperty(ref _hourlyRate, value); }

    public string? SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public IAsyncRelayCommand CreateCommand { get; }

    private Task CreateAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        if (string.IsNullOrWhiteSpace(LicensePlate))
        {
            throw new InvalidOperationException("Araç plakası zorunludur.");
        }

        var request = new CreateTicketRequest(
            _branchContext.ActiveBranchId ?? _session.User?.BranchId,
            LicensePlate,
            Brand,
            Model,
            Color,
            CustomerName,
            CustomerPhone,
            KeyTag,
            ParkingSpot,
            Notes,
            (decimal)HourlyRate);

        var ticket = await _api.CreateTicketAsync(request);
        SuccessMessage = $"{ticket.LicensePlate} plakalı araç {ticket.TicketNumber} numarasıyla kaydedildi.";
        ClearForm();
    });

    private void ClearForm()
    {
        LicensePlate = string.Empty;
        Brand = string.Empty;
        Model = string.Empty;
        Color = string.Empty;
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        KeyTag = string.Empty;
        ParkingSpot = string.Empty;
        Notes = string.Empty;
    }
}
