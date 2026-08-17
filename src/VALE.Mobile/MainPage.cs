using Microsoft.Maui.Controls;

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

        _email = UiKit.Entry("E-posta adresi", Keyboard.Email);
        _password = UiKit.Entry("Parola", password: true);
        _password.Completed += async (_, _) => await LoginAsync();

        _login = UiKit.PrimaryButton("Giriş Yap");
        _login.Clicked += async (_, _) =>
        {
            if (_busy) _loginCts?.Cancel();
            else await LoginAsync();
        };

        _status = UiKit.Label("Üretim sunucusu hazır olduğunda güvenli giriş yapılır.", 12, false, true);
        _status.HorizontalTextAlignment = TextAlignment.Center;
        _activity = UiKit.Activity();
        _activity.IsVisible = false;

        var register = UiKit.SecondaryButton("Hesap Oluştur");
        register.Clicked += async (_, _) => await Navigation.PushAsync(new RegisterPage(_api));
        var forgot = UiKit.SecondaryButton("Şifremi Unuttum");
        forgot.Clicked += async (_, _) => await Navigation.PushAsync(new ForgotPasswordPage(_api));
        var connection = new Button
        {
            Text = "Bağlantı ayarları",
            BackgroundColor = Colors.Transparent,
            TextColor = ThemeService.Palette.Secondary,
            FontSize = 12,
            Padding = new Thickness(6)
        };
        connection.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(_api));

        var actionGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10
        };
        actionGrid.Add(register, 0, 0);
        actionGrid.Add(forgot, 1, 0);

        var loginCard = UiKit.Card(new VerticalStackLayout
        {
            Spacing = 13,
            Children =
            {
                UiKit.Label("Tekrar hoş geldiniz", 22, true),
                UiKit.Label("Şubenizi, araçları ve tahsilatları tek yerden yönetin.", 13, false, true),
                _email,
                _password,
                _login,
                new HorizontalStackLayout { HorizontalOptions = LayoutOptions.Center, Spacing = 8, Children = { _activity, _status } },
                actionGrid,
                connection
            }
        }, new Thickness(20), 24);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 58, 24, 28),
                Spacing = 24,
                Children =
                {
                    new Image { Source = "vale_logo.svg", HeightRequest = 104, WidthRequest = 104, HorizontalOptions = LayoutOptions.Center },
                    new VerticalStackLayout
                    {
                        Spacing = 3,
                        Children =
                        {
                            new Label { Text = "VALE", FontSize = 42, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = ThemeService.Palette.Text, CharacterSpacing = 3 },
                            new Label { Text = "Akıllı vale operasyon platformu", FontSize = 14, HorizontalTextAlignment = TextAlignment.Center, TextColor = ThemeService.Palette.Secondary }
                        }
                    },
                    loginCard,
                    UiKit.Label("Güvenli oturum • Ortak veri • Çoklu şube • Raporlama", 11, false, true)
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
            _status.Text = "Hesap doğrulanıyor…";
            var login = await _api.LoginAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, _loginCts.Token);
            _status.Text = "Giriş başarılı";
            App.ShowAuthenticated(_api, login.User);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Bağlantı iptal edildi.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlertAsync("Giriş yapılamadı", ex.Message, "Tamam");
        }
        finally
        {
            SetBusy(false);
            _loginCts?.Dispose();
            _loginCts = null;
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _email.IsEnabled = !busy;
        _password.IsEnabled = !busy;
        _activity.IsVisible = busy;
        _activity.IsRunning = busy;
        _login.Text = busy ? "İptal" : "Giriş Yap";
        _login.BackgroundColor = busy ? ThemeService.Palette.SoftCard : ThemeService.Palette.Accent;
        _login.TextColor = busy ? ThemeService.Palette.Text : Colors.White;
    }
}
