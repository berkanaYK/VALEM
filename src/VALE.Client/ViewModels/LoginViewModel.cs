using CommunityToolkit.Mvvm.Input;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SessionService _session;
    private readonly BranchContext _branchContext;
    private string _email = string.Empty;
    private string _password = string.Empty;

    public LoginViewModel(ApiClient api, SessionService session, BranchContext branchContext)
    {
        _api = api;
        _session = session;
        _branchContext = branchContext;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
    }

    public event Action<UserDto>? LoginSucceeded;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public IAsyncRelayCommand LoginCommand { get; }

    private Task LoginAsync() => ExecuteAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("E-posta adresi ve parola zorunludur.");
        }

        var response = await _api.LoginAsync(new LoginRequest(Email.Trim(), Password));
        _session.Set(response);
        _branchContext.Initialize(response.User);
        Password = string.Empty;
        LoginSucceeded?.Invoke(response.User);
    });
}
