using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class MainPage : ContentPage
{
    private readonly ApiClient _api = new();
    private readonly Entry _email;
    private readonly Entry _password;
    private readonly Button _login;
    private readonly Label _status;
    private readonly ActivityIndicator _activity;
    private CancellationTokenSource? _loginCts;
    private bool _busy;

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

        _login = UiKit.PrimaryButton("Giriş Yap");
        _login.AutomationId = "login-submit";
        _login.Clicked += async (_, _) => { if (_busy) _loginCts?.Cancel(); else await LoginAsync(); };

        var emailCode = UiKit.SecondaryButton("E-posta Koduyla Giriş");
        emailCode.Clicked += async (_, _) => await Navigation.PushAsync(new EmailCodeLoginPage(_api));
        var register = UiKit.SecondaryButton("Yeni Hesap Oluştur");
        register.AutomationId = "register-open";
        register.Clicked += async (_, _) => await Navigation.PushAsync(new TenantRegisterPage(_api));
        var forgot = UiKit.TextButton("Parolamı unuttum");
        forgot.Clicked += async (_, _) => await Navigation.PushAsync(new ForgotPasswordPage(_api));
        var connection = UiKit.TextButton("Bağlantı ayarları");
        connection.FontSize = 12;
        connection.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(_api));

        _status = UiKit.Label("Hesabınızla giriş yapın. Sunucu bağlantısı otomatik kontrol edilir.", 11.5, false, true);
        _status.MaxLines = 4;
        _activity = UiKit.Activity();
        _activity.IsVisible = false;

        var statusRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
        statusRow.Add(_activity, 0, 0); statusRow.Add(_status, 1, 0);

        var loginCard = UiKit.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                UiKit.Label("Hesabınıza giriş yapın", 24, true),
                UiKit.Label("Yetkinize göre araç, tahsilat, rapor ve yönetim ekranlarına erişirsiniz.", 13, false, true),
                UiKit.Label("E-posta", 11, true, true), _email,
                UiKit.Label("Parola", 11, true, true), _password,
                forgot, _login, emailCode, statusRow,
                UiKit.Divider(),
                register, connection
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

    private async Task LoginAsync()
    {
        if (_busy) return;
        _loginCts = new CancellationTokenSource();
        try
        {
            SetBusy(true);
            var progress = new Progress<string>(text => _status.Text = text);
            await _api.EnsureServerReadyAsync(progress, _loginCts.Token);
            _status.Text = "Hesabınız kontrol ediliyor…";
            LoginResponse login;
            try
            {
                login = await _api.LoginAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, _loginCts.Token);
            }
            catch (TwoFactorRequiredException)
            {
                var code = await DisplayPromptAsync("İki adımlı doğrulama", "Authenticator uygulamanızdaki 6 haneli kodu girin.", "Devam Et", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
                if (string.IsNullOrWhiteSpace(code)) return;
                var normalized = code.Trim();
                if (normalized.Length != 6 || !normalized.All(char.IsDigit))
                {
                    await DisplayAlertAsync("Kod geçersiz", "Authenticator uygulamasındaki 6 haneli sayısal kodu girin.", "Tamam");
                    return;
                }
                login = await _api.LoginWithTwoFactorAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, normalized, _loginCts.Token);
            }
            _status.Text = "Giriş başarılı";
            App.ShowAuthenticated(_api, login.User);
        }
        catch (OperationCanceledException) { _status.Text = "İşlem iptal edildi."; }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlertAsync("Giriş yapılamadı", ex.Message, "Tamam");
        }
        finally
        {
            SetBusy(false);
            _loginCts?.Dispose(); _loginCts = null;
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _email.IsEnabled = !busy; _password.IsEnabled = !busy;
        _activity.IsVisible = busy; _activity.IsRunning = busy;
        _login.Text = busy ? "İptal" : "Giriş Yap";
        _login.SetDynamicResource(VisualElement.BackgroundColorProperty, busy ? "ValeSoftCard" : "ValeAccent");
        if (busy)
            _login.SetDynamicResource(Button.TextColorProperty, "ValeText");
        else
            _login.TextColor = Colors.White;
    }
}
