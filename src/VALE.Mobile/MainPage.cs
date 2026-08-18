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
        _email.ReturnType = ReturnType.Next;
        _email.AutomationId = "login-email";

        _password = UiKit.Entry("Parola", password: true);
        _password.ReturnType = ReturnType.Go;
        _password.AutomationId = "login-password";
        _password.Completed += async (_, _) => await LoginAsync();

        _login = UiKit.PrimaryButton("Giriş Yap");
        _login.AutomationId = "login-submit";
        _login.Clicked += async (_, _) =>
        {
            if (_busy) _loginCts?.Cancel();
            else await LoginAsync();
        };

        _status = UiKit.Label("Sunucu bağlantısı giriş sırasında otomatik kontrol edilir.", 11.5, false, true);
        _status.HorizontalTextAlignment = TextAlignment.Start;
        _status.VerticalTextAlignment = TextAlignment.Center;
        _status.MaxLines = 4;
        _status.AutomationId = "login-status";

        _activity = UiKit.Activity();
        _activity.IsVisible = false;

        var register = UiKit.SecondaryButton("Yeni Hesap Oluştur");
        register.AutomationId = "register-open";
        register.Clicked += async (_, _) => await Navigation.PushAsync(new RegisterPage(_api));

        var forgot = UiKit.TextButton("Şifremi unuttum?");
        forgot.AutomationId = "forgot-password-open";
        forgot.FontSize = 13.5;
        forgot.Clicked += async (_, _) => await Navigation.PushAsync(new ForgotPasswordPage(_api));

        var connection = UiKit.TextButton("Bağlantı Ayarları");
        connection.AutomationId = "connection-settings-open";
        connection.FontSize = 12;
        connection.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(_api));

        var statusRow = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        statusRow.Add(_activity, 0, 0);
        statusRow.Add(_status, 1, 0);

        var loginCard = UiKit.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                UiKit.Label("Tekrar hoş geldiniz", 24, true),
                UiKit.Label("VALE hesabınızla güvenli şekilde devam edin.", 13, false, true),
                UiKit.Label("E-posta", 11, true, true),
                _email,
                UiKit.Label("Parola", 11, true, true),
                _password,
                forgot,
                _login,
                statusRow,
                new BoxView { HeightRequest = 1, BackgroundColor = ThemeService.Palette.Border, Margin = new Thickness(0, 4) },
                register,
                connection
            }
        }, new Thickness(18), 24);
        loginCard.MaximumWidthRequest = 520;
        loginCard.HorizontalOptions = LayoutOptions.Fill;

        var brandTitle = new Label
        {
            Text = "VALE",
            FontSize = 38,
            FontAttributes = FontAttributes.Bold,
            FontAutoScalingEnabled = true,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeService.Palette.Text,
            CharacterSpacing = 2.4,
            LineBreakMode = LineBreakMode.NoWrap
        };

        var brandSubtitle = new Label
        {
            Text = "Akıllı vale operasyon platformu",
            FontSize = 13.5,
            FontAutoScalingEnabled = true,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeService.Palette.Secondary,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var footer = new Label
        {
            Text = "Güvenli oturum  •  Ortak veri  •  Çoklu şube  •  Raporlama",
            FontSize = 10.5,
            FontAutoScalingEnabled = true,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeService.Palette.Secondary,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        };

        var root = new VerticalStackLayout
        {
            Padding = new Thickness(16, 34, 16, 24),
            Spacing = 16,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new Image { Source = "vale_logo.svg", HeightRequest = 78, WidthRequest = 78, HorizontalOptions = LayoutOptions.Center },
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { brandTitle, brandSubtitle }
                },
                loginCard,
                footer
            }
        };

        Content = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = root
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
