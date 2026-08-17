using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class AdministrationViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private string _branchCode = string.Empty;
    private string _branchName = string.Empty;
    private string _branchCity = string.Empty;
    private string _branchAddress = string.Empty;
    private string _userFullName = string.Empty;
    private string _userEmail = string.Empty;
    private string _userPassword = string.Empty;
    private BranchDto? _selectedBranch;
    private string _selectedRole = "Valet";
    private string? _successMessage;

    public AdministrationViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateBranchCommand = new AsyncRelayCommand(CreateBranchAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
    }

    public ObservableCollection<BranchDto> Branches { get; } = [];
    public ObservableCollection<AdminUserDto> Users { get; } = [];
    public IReadOnlyList<string> AvailableRoles { get; } = ["Valet", "Cashier", "Manager", "Admin"];

    public string BranchCode { get => _branchCode; set => SetProperty(ref _branchCode, value); }
    public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }
    public string BranchCity { get => _branchCity; set => SetProperty(ref _branchCity, value); }
    public string BranchAddress { get => _branchAddress; set => SetProperty(ref _branchAddress, value); }
    public string UserFullName { get => _userFullName; set => SetProperty(ref _userFullName, value); }
    public string UserEmail { get => _userEmail; set => SetProperty(ref _userEmail, value); }
    public string UserPassword { get => _userPassword; set => SetProperty(ref _userPassword, value); }
    public BranchDto? SelectedBranch { get => _selectedBranch; set => SetProperty(ref _selectedBranch, value); }
    public string SelectedRole { get => _selectedRole; set => SetProperty(ref _selectedRole, value); }

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
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand CreateBranchCommand { get; }
    public IAsyncRelayCommand CreateUserCommand { get; }

    public Task LoadAsync() => ExecuteAsync(LoadCoreAsync);

    private Task CreateBranchAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        var branch = await _api.CreateBranchAsync(new CreateBranchRequest(
            BranchCode,
            BranchName,
            BranchCity,
            BranchAddress));
        SuccessMessage = $"{branch.Name} oluşturuldu.";
        BranchCode = string.Empty;
        BranchName = string.Empty;
        BranchCity = string.Empty;
        BranchAddress = string.Empty;
        await LoadCoreAsync();
    });

    private Task CreateUserAsync() => ExecuteAsync(async () =>
    {
        SuccessMessage = null;
        var branch = SelectedBranch ?? throw new InvalidOperationException("Kullanıcı için bir şube seçin.");
        var user = await _api.CreateUserAsync(new CreateUserRequest(
            UserEmail,
            UserPassword,
            UserFullName,
            branch.Id,
            [SelectedRole]));
        SuccessMessage = $"{user.FullName} kullanıcısı oluşturuldu.";
        UserFullName = string.Empty;
        UserEmail = string.Empty;
        UserPassword = string.Empty;
        await LoadCoreAsync();
    });

    private async Task LoadCoreAsync()
    {
        var branches = await _api.GetBranchesAsync();
        var users = await _api.GetUsersAsync();

        var selectedBranchId = SelectedBranch?.Id;
        Branches.Clear();
        foreach (var branch in branches)
        {
            Branches.Add(branch);
        }

        SelectedBranch = Branches.FirstOrDefault(x => x.Id == selectedBranchId) ?? Branches.FirstOrDefault();

        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(user);
        }
    }
}

