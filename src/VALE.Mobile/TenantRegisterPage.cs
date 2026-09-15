using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class TenantRegisterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Picker _accountType = UiKit.Picker("Hesap türü");
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _email = UiKit.Entry("E-posta", Keyboard.Email);
    private readonly Entry _username = UiKit.Entry("Kullanıcı adı");
    private readonly Entry _phone = UiKit.Entry("Telefon (isteğe bağlı)", Keyboard.Telephone);
    private readonly Entry _password = UiKit.Entry("Parola", password: true);
    private readonly Entry _repeat = UiKit.Entry("Parolayı tekrar girin", password: true);
    private readonly VerticalStackLayout _passwordFields = new() { Spacing = 10 };
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
        _name.AutomationId = "register-name";
        _email.AutomationId = "register-email";
        _username.AutomationId = "register-username";
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

        _passwordFields.Add(UiKit.PasswordField(_password, "register-password-visibility"));
        _passwordFields.Add(UiKit.PasswordField(_repeat, "register-password-repeat-visibility"));
        _passwordFields.Add(UiKit.Label("Parola 6-20 karakter olmalı; en az bir büyük harf, küçük harf, rakam ve özel karakter içermeli.", 11, false, true));

        _ownerFields.Children.Add(UiKit.Label("Firma kodu ve Merkez şubesi otomatik oluşturulur.", 11, false, true));
        var ownerAdvancedFields = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                UiKit.Label("Yalnızca özel kodlandırma kullanıyorsanız doldurun.", 11, false, true),
                UiKit.Field(_companyCode), UiKit.Field(_branchName), UiKit.Field(_branchCode), UiKit.Field(_city)
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
        _staffFields.Children.Add(UiKit.Field(_employeeCode));
        _joinFields.Add(UiKit.Label("Firma ve şube kodunu yöneticinizden alın. Başvurunuz onaylandığında firmanın izin verilen kayıtlarına erişebilirsiniz. Davet kodu gerekmez.", 11.5, false, true));
        _joinFields.Add(UiKit.Field(_joinCompanyCode));
        _joinFields.Add(UiKit.Field(_joinBranchCode));

        var save = UiKit.PrimaryButton("Hesabı Oluştur");
        save.AutomationId = "register-submit";
        save.Clicked += async (_, _) => await SaveAsync(save);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 20, 18, 30),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("VALE hesabı oluşturun", 27, true),
                    UiKit.Label("E-posta doğrulamasından sonra e-posta adresiniz veya kullanıcı adınız ve parolanızla giriş yapabilirsiniz.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Hesap türü", 11, true, true),
                            _accountType,
                            UiKit.Field(_name),
                            UiKit.Field(_email),
                            UiKit.Field(_username),
                            UiKit.Label("Kullanıcı adı 3-30 karakter olabilir; harf, rakam, nokta, alt çizgi ve kısa çizgi kullanabilirsiniz.", 11, false, true),
                            _passwordFields,
                            UiKit.Field(_phone),
                            UiKit.Field(_companyName),
                            _ownerFields,
                            _staffFields,
                            _joinFields,
                            UiKit.Label("E-posta koduyla giriş seçeneği giriş ekranında ayrıca bulunur. Mevcut firmaya katılım için e-posta doğrulaması ve yönetici onayı gerekir.", 11, false, true),
                            save
                        }
                    })
                }
            }
        };
        UpdateMode();
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
    }

    private async Task SaveAsync(Button save)
    {
        const string loginMethod = LoginMethods.Password;
        if (_password.Text != _repeat.Text)
        {
            await DisplayAlertAsync("Parola", "Parolalar aynı değil.", "Tamam");
            return;
        }
        if (!IsStrongPassword(_password.Text))
        {
            await DisplayAlertAsync("Parola", "Parola 6-20 karakter olmalı; en az bir büyük harf, küçük harf, rakam ve özel karakter içermelidir.", "Tamam");
            return;
        }
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_username.Text))
        {
            await DisplayAlertAsync("Eksik bilgi", "Ad soyad, e-posta ve kullanıcı adı alanlarını doldurun.", "Tamam");
            return;
        }
        if (!IsValidUsername(_username.Text))
        {
            await DisplayAlertAsync("Kullanıcı adı", "Kullanıcı adı 3-30 karakter olmalı ve yalnızca harf, rakam, nokta, alt çizgi veya kısa çizgi içermelidir.", "Tamam");
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
                    _name.Text.Trim(), _email.Text.Trim(), _password.Text,
                    N(_phone.Text), N(_joinCompanyCode.Text), N(_joinBranchCode.Text), null, null, loginMethod, null, _username.Text.Trim()));
            }
            else if (_accountType.SelectedIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(_companyName.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Yalnızca firma adını yazmanız yeterli.", "Tamam");
                    return;
                }
                result = await _api.RegisterOwnerAsync(new OwnerRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), _password.Text,
                    N(_phone.Text), _companyName.Text.Trim(), N(_companyCode.Text),
                    N(_branchName.Text), N(_branchCode.Text), N(_city.Text), loginMethod, _username.Text.Trim()));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_companyName.Text))
                {
                    await DisplayAlertAsync("Firma bilgileri", "Deneme hesabınız için bir firma adı yazın.", "Tamam");
                    return;
                }
                result = await _api.RegisterStaffAsync(new StaffRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), _password.Text, N(_phone.Text),
                    null, null, null, N(_employeeCode.Text), loginMethod, _companyName.Text.Trim(), _username.Text.Trim()));
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
    private static bool IsStrongPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length is >= 6 and <= 20 &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) &&
        password.Any(ch => !char.IsLetterOrDigit(ch));

    private static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username) && username.Trim().Length is >= 3 and <= 30 &&
        username.Trim().All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');
}
