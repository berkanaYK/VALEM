namespace VALE.Mobile;

public sealed class SmsLoginPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Button _send;
    private readonly Label _status;
    public SmsLoginPage(ApiClient api, bool remember, bool verifyPhone = false, string? initialPhone = null)
    {
        _api = api; Title = verifyPhone ? "Telefonu Doğrula" : "OTP / SMS ile Giriş"; UiKit.StylePage(this);
        var phone = UiKit.Entry("Telefon (+90 5xx xxx xx xx)", Keyboard.Telephone); phone.Text = initialPhone;
        var code = UiKit.Entry("6 haneli SMS kodu", Keyboard.Numeric); code.MaxLength = 6;
        var totp = UiKit.Entry("Authenticator kodu (istenirse)", Keyboard.Numeric); totp.IsVisible = false;
        _status = UiKit.Label("SMS hizmeti kontrol ediliyor…", 12, false, true);
        _send = UiKit.PrimaryButton("SMS Kodunu Gönder"); _send.IsEnabled = false;
        var verify = UiKit.PrimaryButton(verifyPhone ? "Telefonu Doğrula" : "Giriş Yap"); verify.IsVisible = false;
        _send.Clicked += async (_, _) =>
        {
            try
            {
                _send.IsEnabled = verify.IsEnabled = false;
                await api.RequestSmsAsync(phone.Text ?? "", verifyPhone);
                phone.IsReadOnly = true; verify.IsVisible = true;
                _status.Text = "Numaranız uygun bir hesaba bağlıysa kod gönderildi. Son kod 10 dakika geçerlidir.";
            }
            catch (Exception ex) { await DisplayAlertAsync("SMS gönderilemedi", UserMessages.For(ex), "Tamam"); }
            finally { _send.IsEnabled = verify.IsEnabled = true; }
        };
        verify.Clicked += async (_, _) =>
        {
            try
            {
                verify.IsEnabled = _send.IsEnabled = false;
                var user = await api.VerifySmsAsync(phone.Text ?? "", code.Text ?? "", totp.Text, remember, verifyPhone);
                if (user is not null) App.ShowAuthenticated(api, user);
                else { await DisplayAlertAsync("Telefon doğrulandı", "Artık bu numarayla SMS girişi yapabilirsiniz.", "Tamam"); await Navigation.PopAsync(); }
            }
            catch (TwoFactorRequiredException) { totp.IsVisible = true; _status.Text = "Hesabınız iki adımlı doğrulamayla korunuyor. Authenticator kodunu da yazın."; }
            catch (Exception ex) { await DisplayAlertAsync("Doğrulama yapılamadı", UserMessages.For(ex), "Tamam"); }
            finally { verify.IsEnabled = _send.IsEnabled = true; }
        };
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 14, Children = { UiKit.Label(Title, 26, true), UiKit.Label("SMS girişi için numaranızı önce Profil > Telefonu Doğrula bölümünde doğrulamalısınız.", 13, false, true), UiKit.Field(phone), _send, _status, UiKit.Field(code), totp, verify } } };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { _send.IsEnabled = await _api.IsSmsAvailableAsync(); _status.Text = _send.IsEnabled ? "Telefon numaranızı girip SMS kodu isteyin." : "SMS ile giriş henüz etkin değil. E-posta, parola veya Authenticator ile giriş yapabilirsiniz."; }
        catch (Exception ex) { _status.Text = UserMessages.For(ex); }
    }
}
