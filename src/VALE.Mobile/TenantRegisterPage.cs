using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class TenantRegisterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Picker _accountType = UiKit.Picker("Hesap türü");
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _email = UiKit.Entry("E-posta", Keyboard.Email);
    private readonly Entry _phone = UiKit.Entry("Telefon (isteğe bağlı)", Keyboard.Telephone);
    private readonly Entry _password = UiKit.Entry("Parola", password: true);
    private readonly Entry _repeat = UiKit.Entry("Parolayı tekrar girin", password: true);
    private readonly VerticalStackLayout _ownerFields = new() { Spacing = 10 };
    private readonly VerticalStackLayout _staffFields = new() { Spacing = 10 };
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

        _ownerFields.Children.Add(_companyName);
        _ownerFields.Children.Add(_companyCode);
        _ownerFields.Children.Add(_branchName);
        _ownerFields.Children.Add(_branchCode);
        _ownerFields.Children.Add(_city);

        _staffFields.Children.Add(UiKit.Label("Yöneticiniz size davet kodu verdiyse yalnızca davet kodunu kullanabilirsiniz. Davet kodunuz yoksa firma ve şube kodunu birlikte girin.", 11.5, false, true));
        _staffFields.Children.Add(_inviteCode);
        _staffFields.Children.Add(_staffCompanyCode);
        _staffFields.Children.Add(_staffBranchCode);
        _staffFields.Children.Add(_employeeCode);

        var save = UiKit.PrimaryButton("Hesabı Oluştur");
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
                    UiKit.Label("Önce hesap türünü seçin. Firma sahibi yeni bir firma açar; personel mevcut firmanın yöneticisine onay başvurusu gönderir.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Hesap türü", 11, true, true),
                            _accountType,
                            _name,
                            _email,
                            _phone,
                            _ownerFields,
                            _staffFields,
                            _password,
                            _repeat,
                            UiKit.Label("Parola en az 10 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermeli.", 11, false, true),
                            save
                        }
                    })
                }
            }
        };
        UpdateMode();
    }

    private void UpdateMode()
    {
        var owner = _accountType.SelectedIndex != 1;
        _ownerFields.IsVisible = owner;
        _staffFields.IsVisible = !owner;
    }

    private async Task SaveAsync(Button save)
    {
        if (_password.Text != _repeat.Text)
        {
            await DisplayAlertAsync("Parola", "Parolalar aynı değil.", "Tamam");
            return;
        }
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            await DisplayAlertAsync("Eksik bilgi", "Ad soyad, e-posta ve parola alanlarını doldurun.", "Tamam");
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
                    _name.Text.Trim(), _email.Text.Trim(), _password.Text,
                    N(_phone.Text), _companyName.Text.Trim(), _companyCode.Text.Trim(),
                    _branchName.Text.Trim(), _branchCode.Text.Trim(), N(_city.Text)));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_inviteCode.Text) && (string.IsNullOrWhiteSpace(_staffCompanyCode.Text) || string.IsNullOrWhiteSpace(_staffBranchCode.Text)))
                {
                    await DisplayAlertAsync("Firma / şube", "Davet kodu veya firma kodu + şube kodu girin.", "Tamam");
                    return;
                }
                result = await _api.RegisterStaffAsync(new StaffRegisterRequest(
                    _name.Text.Trim(), _email.Text.Trim(), _password.Text, N(_phone.Text),
                    N(_staffCompanyCode.Text), N(_staffBranchCode.Text), N(_inviteCode.Text), N(_employeeCode.Text)));
            }

            await DisplayAlertAsync(result.RequiresApproval ? "Başvuru alındı" : "Hesap hazır", result.Message, "Tamam");
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
