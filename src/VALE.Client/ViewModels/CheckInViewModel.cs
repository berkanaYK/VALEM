using System.Collections.ObjectModel;
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
    private string? _selectedBrand;
    private string? _selectedModel;
    private string _manualBrand = string.Empty;
    private string _manualModel = string.Empty;
    private string _color = string.Empty;
    private double _year = DateTime.Today.Year;
    private string? _fuelType;
    private string? _transmission;
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _keyTag = string.Empty;
    private string _parkingSpot = string.Empty;
    private string _notes = string.Empty;
    private double _hourlyRate = 100d;
    private string? _photoBase64;
    private string _photoFileName = "Fotoğraf eklenmedi";
    private string? _successMessage;

    public CheckInViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
    }

    public IReadOnlyList<string> Brands => VehicleCatalog.Brands;
    public IReadOnlyList<string> FuelTypes => VehicleCatalog.FuelTypes;
    public IReadOnlyList<string> Transmissions => VehicleCatalog.Transmissions;
    public ObservableCollection<string> Models { get; } = [];

    public string LicensePlate { get => _licensePlate; set => SetProperty(ref _licensePlate, value); }

    public string? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (!SetProperty(ref _selectedBrand, value)) return;
            Models.Clear();
            foreach (var item in VehicleCatalog.ModelsFor(value)) Models.Add(item);
            SelectedModel = null;
            OnPropertyChanged(nameof(IsManualVehicle));
        }
    }

    public string? SelectedModel { get => _selectedModel; set => SetProperty(ref _selectedModel, value); }
    public string ManualBrand { get => _manualBrand; set => SetProperty(ref _manualBrand, value); }
    public string ManualModel { get => _manualModel; set => SetProperty(ref _manualModel, value); }
    public bool IsManualVehicle => string.Equals(SelectedBrand, "Diğer", StringComparison.OrdinalIgnoreCase);
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public double Year { get => _year; set => SetProperty(ref _year, value); }
    public string? FuelType { get => _fuelType; set => SetProperty(ref _fuelType, value); }
    public string? Transmission { get => _transmission; set => SetProperty(ref _transmission, value); }
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }
    public string KeyTag { get => _keyTag; set => SetProperty(ref _keyTag, value); }
    public string ParkingSpot { get => _parkingSpot; set => SetProperty(ref _parkingSpot, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public double HourlyRate { get => _hourlyRate; set => SetProperty(ref _hourlyRate, value); }
    public string PhotoFileName { get => _photoFileName; private set => SetProperty(ref _photoFileName, value); }
    public bool HasPhoto => !string.IsNullOrWhiteSpace(_photoBase64);

    public string? SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess));
        }
    }

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public IAsyncRelayCommand CreateCommand { get; }

    public void SetPhoto(string base64, string fileName)
    {
        _photoBase64 = base64;
        PhotoFileName = fileName;
        OnPropertyChanged(nameof(HasPhoto));
    }

    public void ClearPhoto()
    {
        _photoBase64 = null;
        PhotoFileName = "Fotoğraf eklenmedi";
        OnPropertyChanged(nameof(HasPhoto));
    }

    private Task CreateAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        var plate = LicensePlate.Trim().ToUpperInvariant();
        if (plate.Length < 3) throw new InvalidOperationException("Geçerli bir araç plakası girin.");
        if (Year is < 1900 or > 2100) throw new InvalidOperationException("Model yılı 1900-2100 arasında olmalıdır.");
        if (HourlyRate <= 0) throw new InvalidOperationException("Saatlik ücret sıfırdan büyük olmalıdır.");

        var brand = IsManualVehicle ? Normalize(ManualBrand) : Normalize(SelectedBrand);
        var model = IsManualVehicle ? Normalize(ManualModel) : Normalize(SelectedModel);
        var request = new CreateTicketRequest(
            _branchContext.ActiveBranchId ?? _session.User?.BranchId,
            plate,
            brand,
            model,
            Normalize(Color),
            Normalize(CustomerName),
            Normalize(CustomerPhone),
            Normalize(KeyTag),
            Normalize(ParkingSpot),
            Normalize(Notes),
            (decimal)HourlyRate,
            (int)Math.Round(Year),
            Normalize(FuelType),
            Normalize(Transmission),
            _photoBase64);

        var ticket = await _api.CreateTicketAsync(request);
        SuccessMessage = $"{ticket.LicensePlate} plakalı araç {ticket.TicketNumber} numarasıyla kaydedildi.";
        ClearForm();
    });

    private void ClearForm()
    {
        LicensePlate = string.Empty;
        SelectedBrand = null;
        ManualBrand = string.Empty;
        ManualModel = string.Empty;
        Color = string.Empty;
        Year = DateTime.Today.Year;
        FuelType = null;
        Transmission = null;
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        KeyTag = string.Empty;
        ParkingSpot = string.Empty;
        Notes = string.Empty;
        ClearPhoto();
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
