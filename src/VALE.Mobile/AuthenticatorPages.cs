using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using QRCoder;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class AuthenticatorLoginPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Entry _email = UiKit.Entry("E-posta adresiniz", Keyboard.Email);
    private readonly Entry _password = UiKit.Entry("Parolanız", password: true);
    private readonly Entry _code = UiKit.Entry("6 haneli Authenticator kodu", Keyboard.Numeric);
    private readonly Label _status = UiKit.Label("Google Authenticator veya başka bir TOTP uygulamasındaki güncel kodu kullanın.", 11.5, false, true);

    public AuthenticatorLoginPage(ApiClient api, string? email = null)
    {
        _api = api;
        Title = "Authenticator ile Giriş";
        UiKit.StylePage(this);
        _email.Text = email;
        _code.MaxLength = 6;

        var login = UiKit.PrimaryButton("OTP ile Güvenli Giriş");
        login.AutomationId = "authenticator-login-submit";
        login.Clicked += async (_, _) => await LoginAsync(login);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 22, 18, 30),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("Authenticator ile giriş", 27, true),
                    UiKit.Label("Bu seçenek hesabınızda 2FA etkinse parola + zamana bağlı tek kullanımlık kod ile giriş yapar.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("E-posta", 10.5, true, true), _email,
                            UiKit.Label("Parola", 10.5, true, true), _password,
                            UiKit.Label("Authenticator kodu", 10.5, true, true), _code,
                            login,
                            _status
                        }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children =
                        {
                            UiKit.Label("İlk kez kullanıyorsanız", 15, true),
                            UiKit.Label("Önce normal giriş yapın ve uygulama içindeki Authenticator Güvenliği ekranından QR kodu Google Authenticator ile okutun. QR kod yalnızca kurulum içindir; sonraki girişlerde 6 haneli OTP yeterlidir.", 11.5, false, true)
                        }
                    })
                }
            }
        };
    }

    private async Task LoginAsync(Button button)
    {
        var code = (_code.Text ?? string.Empty).Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            await DisplayAlertAsync("Kod geçersiz", "6 haneli sayısal Authenticator kodunu girin.", "Tamam");
            return;
        }

        try
        {
            button.IsEnabled = false;
            _status.Text = "Authenticator kodu doğrulanıyor…";
            var login = await _api.LoginWithTwoFactorAsync(_email.Text ?? string.Empty, _password.Text ?? string.Empty, code);
            _status.Text = "Giriş başarılı";
            App.ShowAuthenticated(_api, login.User);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlertAsync("Giriş yapılamadı", ex.Message, "Tamam");
        }
        finally { button.IsEnabled = true; }
    }
}

public sealed class AuthenticatorSecurityPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Label _status = UiKit.Label("Authenticator durumu kontrol ediliyor…", 12.5, false, true);
    private readonly VerticalStackLayout _body = new() { Spacing = 10 };

    public AuthenticatorSecurityPage(ApiClient api)
    {
        _api = api;
        Title = "Authenticator Güvenliği";
        UiKit.StylePage(this);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 20, 18, 30),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("Google Authenticator / TOTP", 27, true),
                    UiKit.Label("QR kodu bir kez okutun. Sonraki girişlerde parolanıza ek olarak her 30 saniyede değişen 6 haneli OTP kodunu kullanın.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { _status, _body } })
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var state = await _api.GetTwoFactorStatusAsync();
            _body.Clear();
            if (state.Enabled)
            {
                _status.Text = $"Authenticator açık • {state.RecoveryCodesLeft} kurtarma kodu kaldı";
                _body.Add(UiKit.Label("Hesabınız OTP ile korunuyor. Giriş ekranındaki “Authenticator ile Giriş” seçeneğini kullanabilirsiniz.", 12, false, true));
                var recovery = UiKit.SecondaryButton("Kurtarma Kodlarını Yenile");
                recovery.Clicked += async (_, _) => await RecoveryAsync();
                var disable = UiKit.TextButton("Authenticator 2FA'yı Kapat");
                disable.TextColor = ThemeService.Palette.Danger;
                disable.Clicked += async (_, _) => await DisableAsync();
                _body.Add(recovery);
                _body.Add(disable);
            }
            else
            {
                _status.Text = "Authenticator kapalı";
                var setup = UiKit.PrimaryButton("QR Kod ile Kurulumu Başlat");
                setup.Clicked += async (_, _) => await SetupAsync();
                _body.Add(setup);
            }
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private async Task SetupAsync()
    {
        try
        {
            _status.Text = "Güvenli kurulum anahtarı hazırlanıyor…";
            var setup = await _api.SetupTwoFactorAsync();
            _body.Clear();

            var qrBytes = BuildQr(setup.AuthenticatorUri);
            var qr = new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(qrBytes)),
                HeightRequest = 260,
                WidthRequest = 260,
                HorizontalOptions = LayoutOptions.Center,
                Aspect = Aspect.AspectFit
            };
            var key = UiKit.Label(setup.SharedKey, 15, true);
            key.LineBreakMode = LineBreakMode.CharacterWrap;
            key.HorizontalTextAlignment = TextAlignment.Center;
            var copy = UiKit.SecondaryButton("Kurulum Anahtarını Kopyala");
            copy.Clicked += async (_, _) => await Clipboard.Default.SetTextAsync(setup.SharedKey);
            var code = UiKit.Entry("Google Authenticator'daki 6 haneli kod", Keyboard.Numeric);
            code.MaxLength = 6;
            var enable = UiKit.PrimaryButton("OTP Kodunu Doğrula ve Aç");
            enable.Clicked += async (_, _) =>
            {
                var value = (code.Text ?? string.Empty).Trim();
                if (value.Length != 6 || !value.All(char.IsDigit))
                {
                    await DisplayAlertAsync("Kod geçersiz", "Authenticator uygulamasındaki 6 haneli kodu girin.", "Tamam");
                    return;
                }
                try
                {
                    enable.IsEnabled = false;
                    var result = await _api.EnableTwoFactorAsync(value);
                    await ShowRecoveryAsync(result.RecoveryCodes);
                    await RefreshAsync();
                }
                catch (Exception ex) { await DisplayAlertAsync("Authenticator açılamadı", ex.Message, "Tamam"); }
                finally { enable.IsEnabled = true; }
            };

            _body.Add(UiKit.Label("1. Google Authenticator'ı açıp + → QR kodu tara seçeneğine basın.", 12.5));
            _body.Add(qr);
            _body.Add(UiKit.Label("QR okunmazsa aşağıdaki anahtarı elle girebilirsiniz:", 11.5, false, true));
            _body.Add(key);
            _body.Add(copy);
            _body.Add(UiKit.Label("2. Uygulamada oluşan 6 haneli kodu aşağıya girin.", 12.5));
            _body.Add(code);
            _body.Add(enable);
            _status.Text = "QR kod hazır • doğrulama bekleniyor";
        }
        catch (Exception ex)
        {
            _status.Text = "Kurulum başlatılamadı";
            await DisplayAlertAsync("Authenticator kurulumu", ex.Message, "Tamam");
        }
    }

    private async Task RecoveryAsync()
    {
        var code = await DisplayPromptAsync("Kurtarma kodları", "Güncel Authenticator kodunuzu girin.", "Yenile", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            var result = await _api.RegenerateRecoveryCodesAsync(code.Trim());
            await ShowRecoveryAsync(result.RecoveryCodes);
            await RefreshAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Kurtarma kodları", ex.Message, "Tamam"); }
    }

    private async Task DisableAsync()
    {
        var code = await DisplayPromptAsync("Authenticator'ı kapat", "Güncel 6 haneli kodunuzu girin.", "Kapat", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;
        var confirmed = await DisplayAlertAsync("Güvenlik uyarısı", "Authenticator korumasını kapatmak istediğinize emin misiniz?", "Evet, kapat", "Vazgeç");
        if (!confirmed) return;
        try
        {
            await _api.DisableTwoFactorAsync(code.Trim());
            await RefreshAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Authenticator kapatılamadı", ex.Message, "Tamam"); }
    }

    private async Task ShowRecoveryAsync(IReadOnlyList<string> codes)
    {
        var text = string.Join(Environment.NewLine, codes);
        await Clipboard.Default.SetTextAsync(text);
        await DisplayAlertAsync("Kurtarma kodlarınız", $"Bu kodları parola yöneticisi gibi güvenli bir yerde saklayın. Her kod tek kullanımlıktır. Kodlar panoya kopyalandı.\n\n{text}", "Tamam");
    }

    private static byte[] BuildQr(string payload)
    {
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(14, drawQuietZones: true);
    }
}
