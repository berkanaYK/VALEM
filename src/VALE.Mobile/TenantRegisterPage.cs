using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class TenantRegisterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Picker _accountType = UiKit.Picker("Hesap türü");
    private readonly Picker _loginMethod = UiKit.Picker("Giriş yöntemi");
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _email = UiKit.Entry("E-posta", Keyboard.Email);
    private readonly Entry _phone = UiKit.Entry("Telefon (isteğe bağlı)", Keyboard.Telephone);
    private readonly Entry _password = UiKit.Entry("Parola", password: true);
    private readonly Entry _repeat = UiKit.Entry("Parolayı tekrar girin", password: true);
    private readonly VerticalStackLayout _passwordFields = new() { Spacing = 10 };
    private readonly Label _loginHint = UiKit.Label(string.Empty, 11.5, false, true);
    private readonly VerticalStackLayout _ownerFields = new() { Spacing = 10 };
    private readonly VerticalStackLayout _staffFields = new() { Spacing = 10 };
    private readonly VerticalStackLayout _staffCodeFields = new() { Spacing = 10 };
    private readonly Entry _companyName = UiKit.Entry("Firma adı");
    private readonly Entry _companyCode = UiKit.Entry("Firma kodu (örn. ACME)");
    private readonly Entry _branchName = UiKit.Entry("İlk şube adı");
    private readonly Entry _branchCode = UiKit.Entry("Şube kodu (örn. 01)");
    private readonly Entry _city = UiKit.Entry("Şehir (isteğe bağlı)");
    private readonly Entry _staffCompanyCode = UiKit.Entry("Firma kodu");
    private readonly Entry _staffBranchCode = UiKit.Entry("Şube kodu");
    private readonly Entry _inviteCode = UiKit.Entry("Davet kodu (varsa)");
    private readonly Entry _employeeCode = UiKit.Entry("Personel kodu (isteğe bağlı)");

    public TenantRegisterPage(ApiClient api)
    {
        _api = api;
        Title = "Hesap Oluştur";
        UiKit.StylePage(this);

        _accountType.ItemsSource = new[] { "Firma sahibi / yönetici", "Personel / mevcut firmaya katıl" };
        _accountType.SelectedIndex = 0;
        _accountType.SelectedIndexChanged += (_, _) => UpdateMode();

        _loginMethod.ItemsSource = new[]
        {
            "E-posta + parola",
            "E-posta koduyla giriş (personel için önerilen)",
            "Parola + Authenticator (2FA)"
        };
        _loginMethod.SelectedIndex = 0;
        _loginMethod.SelectedIndexChanged += (_, _) => UpdateLoginMethod();

        _passwordFields.Add(_password);
        _passwordFields.Add(_repeat);
        _passwordFields.Add(UiKit.Label("Parola en az 10 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermeli.", 11, false, true));

        _ownerFields.Children.Add(_companyName);
        _ownerFields.Children.Add(_companyCode);
        _ownerFields.Children.Add(_branchName);
        _ownerFields.Children.Add(_branchCode);
        _ownerFields.Children.Add(_city);

        _inviteCode.Placeholder = "Yöneticinizden aldığınız davet kodu";
        _staffFields.Children.Add(UiKit.Label("En hızlı katılım", 13, true));
        _staffFields.Children.Add(UiKit.Label("Yöneticinizden aldığınız tek davet kodu yeterlidir. Firma ve şube hesabınıza otomatik bağlanır.", 11.5, false, true));
        _staffFields.Children.Add(_inviteCode);
        _staffCodeFields.Children.Add(UiKit.Label("Davet kodunuz yoksa firma ve şube kodunu birlikte girin. Personel kodu isteğe bağlıdır.", 11, false, true));
        _staffCodeFields.Children.Add(_staffCompanyCode);
        _staffCodeFields.Children.Add(_staffBranchCode);
        _staffCodeFields.Children.Add(_employeeCode);
        var staffCodeCard = UiKit.Card(_staffCodeFields, new Thickness(12), 14);
        staffCodeCard.IsVisible = false;
        var staffCodeToggle = UiKit.TextButton("Davet kodum yok");
        staffCodeToggle.Clicked += (_, _) =>
        {
            staffCodeCard.IsVisible = !staffCodeCard.IsVisible;
            staffCodeToggle.Text = staffCodeCard.IsVisible ? "Firma / şube alanlarını kapat" : "Davet kodum yok";
        };
        _staffFields.Children.Add(staffCodeToggle);
        _staffFields.Children.Add(staffCodeCard);

        var save = UiKit.PrimaryButton("Hesabı Oluştur");
        save.Clicked += async (_, _) => await SaveAsync(save);
        _phone.IsVisible = false;
        var phoneToggle = UiKit.TextButton("＋ Telefon ekle (isteğe bağlı)");
        phoneToggle.Clicked += (_, _) =>
        {
            _phone.IsVisible = !_phone.IsVisible;
            phoneToggle.Text = _phone.IsVisible ? "− Telefon alanını kapat" : "＋ Telefon ekle (isteğe bağlı)";
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 20, 18, 30),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("VALE hesabı oluşturun", 27, true),
                    UiKit.Label("Hesap türünü ve kullanmak istediğiniz giriş yöntemini seçin. İlk kurulumda e-posta adresinize sahiplik doğrulama bağlantısı gönderilir.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Hesap türü", 11, true, true),
                            _accountType,
                            _name,
                            _email,
                            phoneToggle,
                            _phone,
                            _ownerFields,
                            _staffFields,
                            UiKit.Divider(),
                            UiKit.Label("Giriş yöntemi", 11, true, true),
                            _loginMethod,
                            _loginHint,
                            _passwordFields,
                            UiKit.Label("Kayıttan sonra e-postanıza 'Bu e-posta sizin mi?' doğrulama bağlantısı gönderilir. E-posta onaylanmadan hesap girişe açılmaz.", 11, false, true),
                            save
                        }
                    })
                }
            }
        };
        UpdateMode();
        UpdateLoginMethod();
    }

    private void UpdateMode()
    {
        var owner = _accountType.SelectedIndex != 1;
        _ownerFields.IsVisible = owner;
        _staffFields.IsVisible = !owner;
        if (!owner && _loginMethod.SelectedIndex == 0)
            _loginMethod.SelectedIndex = 1;
    }

    private void UpdateLoginMethod()
    {
        var emailCodeOnly = _loginMethod.SelectedIndex == 1;
        _passwordFields.IsVisible = !emailCodeOnly;
        _loginHint.Text = _loginMethod.SelectedIndex switch
        {
            1 => "Parola oluşturmanız gerekmez. Giriş ekranında e-posta adresinize gelen tek kullanımlık 6 haneli kodu kullanırsınız.",
            2 => "Güçlü bir parola oluşturun. E-posta doğrulamasından ve ilk girişten sonra Authenticator Güvenliği ekranındaki QR kodu Google/Microsoft Authenticator ile okutursunuz.",
            _ => "Standart giriş: e-posta adresiniz ve güçlü parolanız. Authenticator zorunlu değildir."
        };
    }

    private string SelectedLoginMethod() => _loginMethod.SelectedIndex switch
    {
        1 => LoginMethods.EmailCode,
        2 => LoginMethods.Authenticator,
        _ => LoginMethods.Password
    };

    private async Task SaveAsync(Button save)
    {
        var loginMethod = SelectedLoginMethod();
        if (loginMethod != LoginMethods.EmailCode)
        {
            if (_password.Text != _repeat.Text)
            {
                await DisplayAlertAsync("Parola", "Parolalar aynı değil.", "Tamam");
                return;
            }
            if (string.IsNullOrWhiteSpace(_password.Text))
            {
                await DisplayAlertAsync("Parola", "Seçtiğiniz giriş yöntemi için güçlü bir parola oluşturun.", "Tamam");
                return;
            }
        }
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_email.Text))
        {
            await DisplayAlertAsync("Eksik bilgi", "Ad soyad ve e-posta alanlarını doldurun.", "Tamam");
            return;
        }

        try
        {
            save.IsEnabled = false;
            await _api.EnsureServerReadyAsync();
            RegisterResponse result;
            if (_accountType.SelectedIndex != 1)
            {
                if (string.IsNullOrWhiteSpace(_companyName.Text) || string.IsNullOrWhiteSpace(_companyCode.Text) || string.IsNullOrWhiteSpace(_branchName.Text) || string.IsNullOrWhiteSpace(_branchCode.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Firma adı/kodu ve ilk şube adı/kodu zorunludur.", "Tamam");
                    return;
                }
                result = await _api.RegisterOwnerAsync(new OwnerRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), loginMethod == LoginMethods.EmailCode ? null : _password.Text,
                    N(_phone.Text), _companyName.Text.Trim(), _companyCode.Text.Trim(),
                    _branchName.Text.Trim(), _branchCode.Text.Trim(), N(_city.Text), loginMethod));
            }
            else
            {
                var inviteCode = N(_inviteCode.Text);
                var companyCode = inviteCode is null ? N(_staffCompanyCode.Text) : null;
                var branchCode = inviteCode is null ? N(_staffBranchCode.Text) : null;
                if (inviteCode is null && (companyCode is null || branchCode is null))
                {
                    await DisplayAlertAsync("Firma / şube", "Davet kodu veya firma kodu + şube kodu girin.", "Tamam");
                    return;
                }
                result = await _api.RegisterStaffAsync(new StaffRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), loginMethod == LoginMethods.EmailCode ? null : _password.Text, N(_phone.Text),
                    companyCode, branchCode, inviteCode, N(_employeeCode.Text), loginMethod));
            }

            await DisplayAlertAsync(result.RequiresApproval ? "Başvuru ve e-posta doğrulama" : "E-posta doğrulama gerekli", result.Message, "Tamam");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Hesap oluşturulamadı", ex.Message, "Tamam");
        }
        finally
        {
            save.IsEnabled = true;
        }
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
