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
    private readonly Entry _companyName = UiKit.Entry("Firma adı");
    private readonly Entry _companyCode = UiKit.Entry("Firma kodu (örn. ACME)");
    private readonly Entry _branchName = UiKit.Entry("İlk şube adı");
    private readonly Entry _branchCode = UiKit.Entry("Şube kodu (örn. 01)");
    private readonly Entry _city = UiKit.Entry("Şehir (isteğe bağlı)");
    private readonly Entry _employeeCode = UiKit.Entry("Personel kodu (isteğe bağlı)");
    private readonly VerticalStackLayout _joinFields = new() { Spacing = 10 };
    private readonly Entry _joinCompanyCode = UiKit.Entry("Yöneticinizin paylaştığı firma kodu");
    private readonly Entry _joinBranchCode = UiKit.Entry("Çalışacağınız şubenin kodu");

    public TenantRegisterPage(ApiClient api)
    {
        _api = api;
        Title = "Hesap Oluştur";
        UiKit.StylePage(this);

        _accountType.AutomationId = "register-account-type";
        _loginMethod.AutomationId = "register-login-method";
        _name.AutomationId = "register-name";
        _email.AutomationId = "register-email";
        _phone.AutomationId = "register-phone";
        _password.AutomationId = "register-password";
        _repeat.AutomationId = "register-password-repeat";
        _companyName.AutomationId = "register-company-name";
        _companyCode.AutomationId = "register-company-code";
        _branchName.AutomationId = "register-branch-name";
        _branchCode.AutomationId = "register-branch-code";
        _city.AutomationId = "register-city";
        _employeeCode.AutomationId = "register-employee-code";

        _accountType.ItemsSource = new[] { "Kendi firmamı oluştur", "Kişisel hesap oluştur", "Mevcut firmama katıl" };
        _accountType.SelectedIndex = 0;
        _accountType.SelectedIndexChanged += (_, _) => UpdateMode();

        _loginMethod.ItemsSource = new[]
        {
            "E-posta + parola",
            "E-posta koduyla giriş (personel için önerilen)",
            "Parola + Authenticator (2FA)"
        };
        _loginMethod.SelectedIndex = 1;
        _loginMethod.SelectedIndexChanged += (_, _) => UpdateLoginMethod();

        _passwordFields.Add(_password);
        _passwordFields.Add(_repeat);
        _passwordFields.Add(UiKit.Label("Parola en az 10 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermeli.", 11, false, true));

        _ownerFields.Children.Add(UiKit.Label("Firma kodu ve Merkez şubesi otomatik oluşturulur.", 11, false, true));
        var ownerAdvancedFields = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                UiKit.Label("Yalnızca özel kodlandırma kullanıyorsanız doldurun.", 11, false, true),
                _companyCode, _branchName, _branchCode, _city
            }
        };
        var ownerAdvancedCard = UiKit.Card(ownerAdvancedFields, new Thickness(12), 14);
        ownerAdvancedCard.IsVisible = false;
        var ownerAdvancedToggle = UiKit.TextButton("Firma ayrıntılarını özelleştir");
        ownerAdvancedToggle.AutomationId = "register-owner-advanced-toggle";
        ownerAdvancedToggle.Clicked += (_, _) =>
        {
            ownerAdvancedCard.IsVisible = !ownerAdvancedCard.IsVisible;
            ownerAdvancedToggle.Text = ownerAdvancedCard.IsVisible ? "Firma ayrıntılarını kapat" : "Firma ayrıntılarını özelleştir";
        };
        _ownerFields.Children.Add(ownerAdvancedToggle);
        _ownerFields.Children.Add(ownerAdvancedCard);

        _staffFields.Children.Add(UiKit.Label("Deneme için istediğiniz firma adını yazabilirsiniz. Size özel firma ve Merkez şube kodları otomatik oluşturulur; davet veya yönetici onayı gerekmez.", 11.5, false, true));
        _staffFields.Children.Add(_employeeCode);
        _joinFields.Add(UiKit.Label("Firma ve şube kodunu yöneticinizden alın. Başvurunuz onaylandığında firmanın izin verilen kayıtlarına erişebilirsiniz. Davet kodu gerekmez.", 11.5, false, true));
        _joinFields.Add(_joinCompanyCode);
        _joinFields.Add(_joinBranchCode);

        var save = UiKit.PrimaryButton("Hesabı Oluştur");
        save.AutomationId = "register-submit";
        save.Clicked += async (_, _) => await SaveAsync(save);
        var signInFields = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                UiKit.Label("Giriş yöntemi", 11, true, true),
                _loginMethod,
                _loginHint,
                _passwordFields,
                _phone
            }
        };
        var signInCard = UiKit.Card(signInFields, new Thickness(12), 14);
        signInCard.IsVisible = false;
        var signInToggle = UiKit.TextButton("Giriş yöntemini veya telefonu değiştir");
        signInToggle.AutomationId = "register-login-options-toggle";
        signInToggle.Clicked += (_, _) =>
        {
            signInCard.IsVisible = !signInCard.IsVisible;
            signInToggle.Text = signInCard.IsVisible ? "Ek seçenekleri kapat" : "Giriş yöntemini veya telefonu değiştir";
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
                    UiKit.Label("Sadece temel bilgileri yazın. Varsayılan olarak parola gerekmez; e-posta koduyla giriş yaparsınız ve daha sonra bu cihazı hatırlatabilirsiniz.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Hesap türü", 11, true, true),
                            _accountType,
                            _name,
                            _email,
                            _companyName,
                            _ownerFields,
                            _staffFields,
                            _joinFields,
                            signInToggle,
                            signInCard,
                            UiKit.Label("Yeni firma ve kişisel hesapla hemen giriş yapabilirsiniz. Mevcut firmaya katılım için e-posta doğrulaması ve yönetici onayı gerekir.", 11, false, true),
                            save
                        }
                    })
                }
            }
        };
        UpdateMode();
        UpdateLoginMethod();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await GuidedTour.ShowRegistrationAsync(this);
    }

    private void UpdateMode()
    {
        var owner = _accountType.SelectedIndex == 0;
        _ownerFields.IsVisible = owner;
        _staffFields.IsVisible = _accountType.SelectedIndex == 1;
        _joinFields.IsVisible = _accountType.SelectedIndex == 2;
        _companyName.IsVisible = _accountType.SelectedIndex != 2;
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
            RegisterResponse result;
            if (_accountType.SelectedIndex == 2)
            {
                if (string.IsNullOrWhiteSpace(_joinCompanyCode.Text) || string.IsNullOrWhiteSpace(_joinBranchCode.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Firma ve şube kodunu doldurun.", "Tamam");
                    return;
                }
                result = await _api.RegisterStaffAsync(new StaffRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), loginMethod == LoginMethods.EmailCode ? null : _password.Text,
                    N(_phone.Text), N(_joinCompanyCode.Text), N(_joinBranchCode.Text), null, null, loginMethod, null));
            }
            else if (_accountType.SelectedIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(_companyName.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Yalnızca firma adını yazmanız yeterli.", "Tamam");
                    return;
                }
                result = await _api.RegisterOwnerAsync(new OwnerRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), loginMethod == LoginMethods.EmailCode ? null : _password.Text,
                    N(_phone.Text), _companyName.Text.Trim(), N(_companyCode.Text),
                    N(_branchName.Text), N(_branchCode.Text), N(_city.Text), loginMethod));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_companyName.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Deneme hesabınız için bir firma adı yazın.", "Tamam");
                    return;
                }
                result = await _api.RegisterStaffAsync(new StaffRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), loginMethod == LoginMethods.EmailCode ? null : _password.Text, N(_phone.Text),
                    null, null, null, N(_employeeCode.Text), loginMethod, _companyName.Text.Trim()));
            }

            await DisplayAlertAsync(result.RequiresApproval ? "Başvuru oluşturuldu" : "Hesap hazır", result.Message, "Tamam");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Hesap oluşturulamadı", UserMessages.For(ex), "Tamam");
        }
        finally
        {
            save.IsEnabled = true;
        }
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
