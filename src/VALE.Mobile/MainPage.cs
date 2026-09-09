using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class MainPage : ContentPage
{
    private const string RememberPreference = "vale_remember_device_preference_v1";
    private readonly ApiClient _api = new();
    private readonly Entry _email;
    private readonly Entry _password;
    private readonly Button _login;
    private readonly Label _status;
    private readonly ActivityIndicator _activity;
    private readonly Switch _rememberDevice;
    private CancellationTokenSource? _loginCts;
    private bool _busy;
    private bool _warmupStarted;
    private bool _restoreAttempted;

    public MainPage()
    {
        Title = "VALE";
        UiKit.StylePage(this);
        NavigationPage.SetHasNavigationBar(this, false);

        _email = UiKit.Entry("E-posta adresiniz", Keyboard.Email);
        _email.ReturnType = ReturnType.Next;
        _email.AutomationId = "login-email";
        _password = UiKit.Entry("Parolanız", password: true);
        _password.ReturnType = ReturnType.Go;
        _password.AutomationId = "login-password";
        _password.Completed += async (_, _) => await LoginAsync();
        _rememberDevice = new Switch
        {
            IsToggled = Preferences.Default.Get(RememberPreference, false),
            OnColor = ThemeService.Palette.Accent,
            AutomationId = "remember-device"
        };

        _login = UiKit.PrimaryButton("Parola ile Giriş");
        _login.AutomationId = "login-submit";
        _login.Clicked += async (_, _) => { if (_busy) _loginCts?.Cancel(); else await LoginAsync(); };

        var authenticator = UiKit.SecondaryButton("Authenticator ile Giriş");
        authenticator.AutomationId = "authenticator-login-open";
        authenticator.Clicked += async (_, _) => await Navigation.PushAsync(new AuthenticatorLoginPage(_api, _email.Text, rememberDevice: _rememberDevice.IsToggled));

        var emailCode = UiKit.SecondaryButton("E-posta Koduyla Giriş");
        emailCode.AutomationId = "email-code-login-open";
        emailCode.Clicked += async (_, _) => await Navigation.PushAsync(new EmailCodeLoginPage(_api, _rememberDevice.IsToggled));
        var resendConfirmation = UiKit.TextButton("E-posta doğrulama linkini tekrar gönder");
        resendConfirmation.Clicked += async (_, _) => await ResendEmailConfirmationAsync(resendConfirmation);
        var register = UiKit.SecondaryButton("Yeni Hesap Oluştur");
        register.AutomationId = "register-open";
        register.Clicked += async (_, _) => await Navigation.PushAsync(new TenantRegisterPage(_api));
        var demo = UiKit.SecondaryButton("Hesap Açmadan İncele");
        demo.AutomationId = "demo-open";
        demo.Clicked += async (_, _) =>
        {
            if (_busy) return;
            try
            {
                _busy = true; demo.IsEnabled = false;
                var user = await _api.StartDemoAsync();
                await DisplayAlertAsync("Deneme hesabı", "Örnek firma üzerinden ekranları inceleyebilirsiniz. Kayıt eklemek ve değiştirmek için kendi hesabınızı oluşturun.", "İncele");
                App.ShowAuthenticated(_api, user);
            }
            catch (Exception ex) { await DisplayAlertAsync("Deneme açılamadı", UserMessages.For(ex), "Tamam"); }
            finally { _busy = false; demo.IsEnabled = true; }
        };
        var forgot = UiKit.TextButton("Parolamı unuttum");
        forgot.Clicked += async (_, _) => await Navigation.PushAsync(new ForgotPasswordPage(_api));
        var connection = UiKit.TextButton("Bağlantı ayarları");
        connection.FontSize = 12;
        connection.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(_api));

        _status = UiKit.Label("Size uygun tek bir giriş yöntemini kullanmanız yeterli.", 11.5, false, true);
        _status.AutomationId = "login-status";
        _status.MaxLines = 4;
        _activity = UiKit.Activity();
        _activity.IsVisible = false;

        var statusRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
        statusRow.Add(_activity, 0, 0); statusRow.Add(_status, 1, 0);

        var rememberRow = new HorizontalStackLayout
        {
            Spacing = 9,
            Children =
            {
                _rememberDevice,
                UiKit.Label("Bu güvenli cihazda oturumu açık tut", 12.5)
            }
        };
        var alternatives = new VerticalStackLayout
        {
            Spacing = 9,
            IsVisible = false,
            Children = { authenticator, resendConfirmation }
        };
        var alternativesToggle = UiKit.SecondaryButton("Diğer giriş seçenekleri");
        alternativesToggle.AutomationId = "login-options-toggle";
        alternativesToggle.Clicked += (_, _) =>
        {
            alternatives.IsVisible = !alternatives.IsVisible;
            alternativesToggle.Text = alternatives.IsVisible ? "Diğer seçenekleri kapat" : "Diğer giriş seçenekleri";
        };

        var loginCard = UiKit.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                UiKit.Label("Hesabınıza giriş yapın", 24, true),
                UiKit.Label("E-posta koduyla parolasız giriş yapabilir veya mevcut parolanızı kullanabilirsiniz.", 13, false, true),
                emailCode,
                UiKit.Divider(),
                UiKit.Label("E-posta", 11, true, true), _email,
                UiKit.Label("Parola", 11, true, true), _password,
                forgot, rememberRow, _login, alternativesToggle, alternatives, statusRow,
                UiKit.Divider(),
                register, demo, connection
            }
        }, new Thickness(18), 24);
        loginCard.MaximumWidthRequest = 520;

        var title = new Label
        {
            Text = "VALE",
            FontSize = 38,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            CharacterSpacing = 2.4
        };
        title.SetDynamicResource(Label.TextColorProperty, "ValeText");

        var footer = new Label
        {
            Text = "Araç operasyonu • Tahsilat • Raporlama • Ekip yönetimi",
            FontSize = 10.5,
            HorizontalTextAlignment = TextAlignment.Center
        };
        footer.SetDynamicResource(Label.TextColorProperty, "ValeSecondary");

        Content = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 34, 16, 24), Spacing = 16,
                Children =
                {
                    new Image { Source = "vale_logo.svg", HeightRequest = 82, WidthRequest = 82, HorizontalOptions = LayoutOptions.Center },
                    title,
                    UiKit.Label("Vale operasyon ve yönetim platformu", 13.5, false, true),
                    loginCard,
                    footer
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_warmupStarted)
        {
            _warmupStarted = true;
            _ = WarmUpServerAsync();
        }
        if (!_restoreAttempted)
        {
            _restoreAttempted = true;
            _status.Text = "Hatırlanan cihaz oturumu kontrol ediliyor…";
            var restored = await _api.TryRestoreSessionAsync();
            if (restored is not null)
            {
                _status.Text = "Oturum yenilendi";
                App.ShowAuthenticated(_api, restored.User);
                return;
            }
            _status.Text = "Giriş yapmaya hazır.";
        }
        try
        {
            _ = await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch
        {
            // Login remains usable if the user denies notification permission.
        }
    }

    private async Task WarmUpServerAsync()
    {
        try
        {
            await _api.TestConnectionAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_busy) _status.Text = "Sunucu hazır • giriş yönteminizi seçebilirsiniz.";
            });
        }
        catch
        {
            // Render/free-tier cold starts can outlive the short warm-up request.
            // The direct login request below is still allowed to continue normally.
        }
    }

    private async Task LoginAsync()
    {
        if (_busy) return;
        _loginCts = new CancellationTokenSource();
        try
        {
            SetBusy(true);
            _status.Text = "Parola ile giriş yapılıyor…";
            LoginResponse login;
            try
            {
                login = await LoginPasswordFlowAsync(_loginCts.Token);
            }
            catch (TwoFactorRequiredException)
            {
                // LoginWithTwoFactorAsync is intentionally executed by AuthenticatorLoginPage so OTP stays a separate step.
                _status.Text = "Bu hesap Authenticator ile korunuyor. OTP adımına geçiliyor…";
                var email = _email.Text ?? string.Empty;
                SetBusy(false);
                await Navigation.PushAsync(new AuthenticatorLoginPage(_api, email, _rememberDevice.IsToggled));
                return;
            }
            _status.Text = "Giriş başarılı";
            App.ShowAuthenticated(_api, login.User);
        }
        catch (OperationCanceledException) { _status.Text = "İşlem iptal edildi."; }
        catch (Exception ex)
        {
            _status.Text = UserMessages.For(ex);
            await DisplayAlertAsync("Giriş yapılamadı", UserMessages.For(ex), "Tamam");
        }
        finally
        {
            SetBusy(false);
            _loginCts?.Dispose(); _loginCts = null;
        }
    }

    private async Task ResendEmailConfirmationAsync(Button button)
    {
        var email = (_email.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            await DisplayAlertAsync("E-posta", "Önce doğrulama linkinin gönderileceği e-posta adresini yazın.", "Tamam");
            return;
        }

        try
        {
            button.IsEnabled = false;
            _status.Text = "Doğrulama bağlantısı isteniyor…";
            using var client = new HttpClient { BaseAddress = new Uri(_api.EffectiveBaseUrl), Timeout = TimeSpan.FromSeconds(25) };
            using var response = await client.PostAsJsonAsync("api/auth/email-confirmation/resend", new EmailCodeRequest(email));
            if (!response.IsSuccessStatusCode)
            {
                _status.Text = "Doğrulama bağlantısı gönderilemedi.";
                await DisplayAlertAsync("E-posta doğrulama", "Doğrulama bağlantısı şu anda gönderilemedi. Biraz sonra tekrar deneyin.", "Tamam");
                return;
            }
            _status.Text = "Doğrulama bağlantısı istendi.";
            await DisplayAlertAsync("E-posta doğrulama", "Hesap kayıtlı ve henüz doğrulanmamışsa yeni doğrulama bağlantısı e-posta adresinize gönderildi.", "Tamam");
        }
        catch (Exception ex)
        {
            _status.Text = "Doğrulama bağlantısı gönderilemedi.";
            await DisplayAlertAsync("E-posta doğrulama", UserMessages.For(ex), "Tamam");
        }
        finally { button.IsEnabled = true; }
    }

    private async Task<LoginResponse> LoginPasswordFlowAsync(CancellationToken ct)
    {
        Preferences.Default.Set(RememberPreference, _rememberDevice.IsToggled);
        try
        {
            return await _api.LoginAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, _rememberDevice.IsToggled, ct);
        }
        catch (InvalidOperationException ex) when (LooksLikeTransientConnection(ex))
        {
            _status.Text = "Sunucu ilk bağlantı için açılıyor… yeniden deneniyor.";
            try { await _api.TestConnectionAsync(ct: ct); } catch { }
            return await _api.LoginAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, _rememberDevice.IsToggled, ct);
        }
    }

    private static bool LooksLikeTransientConnection(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("Sunucu yanıt vermedi", StringComparison.OrdinalIgnoreCase)
            || text.Contains("sunucusuna bağlanılamadı", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _email.IsEnabled = !busy; _password.IsEnabled = !busy; _rememberDevice.IsEnabled = !busy;
        _activity.IsVisible = busy; _activity.IsRunning = busy;
        _login.Text = busy ? "İptal" : "Parola ile Giriş";
        _login.SetDynamicResource(VisualElement.BackgroundColorProperty, busy ? "ValeSoftCard" : "ValeAccent");
        if (busy)
            _login.SetDynamicResource(Button.TextColorProperty, "ValeText");
        else
            _login.TextColor = Colors.White;
    }
}
