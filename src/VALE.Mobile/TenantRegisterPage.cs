using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class TenantRegisterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Picker _kind = UiKit.Picker("Hesap türünü seçin");
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _email = UiKit.Entry("E-posta", Keyboard.Email);
    private readonly Entry _phone = UiKit.Entry("Telefon (isteğe bağlı)", Keyboard.Telephone);
    private readonly Entry _employee = UiKit.Entry("Personel kodu (isteğe bağlı)");
    private readonly Entry _password = UiKit.Entry("Parola", password: true);
    private readonly Entry _repeat = UiKit.Entry("Parolayı tekrar girin", password: true);

    private readonly Entry _companyName = UiKit.Entry("Firma adı");
    private readonly Entry _companyCodeOwner = UiKit.Entry("Firma kodu (örn. CWENERJI)");
    private readonly Entry _branchName = UiKit.Entry("İlk şube adı");
    private readonly Entry _branchCodeOwner = UiKit.Entry("Şube kodu (örn. ANT01)");
    private readonly Entry _branchCity = UiKit.Entry("Şehir");
    private readonly Entry _branchAddress = UiKit.Entry("Adres (isteğe bağlı)");

    private readonly Entry _inviteCode = UiKit.Entry("Davet kodu (varsa)");
    private readonly Entry _companyCodeStaff = UiKit.Entry("Firma kodu");
    private readonly Picker _branchPicker = UiKit.Picker("Şube seçin");
    private readonly Label _companyInfo = UiKit.Label("Firma kodunu girip şubeleri yükleyin veya yöneticinizin davet kodunu kullanın.", 11.5, false, true);
    private readonly VerticalStackLayout _ownerFields = new() { Spacing = 10 };
    private readonly VerticalStackLayout _staffFields = new() { Spacing = 10 };
    private IReadOnlyList<RegistrationBranchDto> _branches = Array.Empty<RegistrationBranchDto>();

    public TenantRegisterPage(ApiClient api)
    {
        _api = api;
        Title = "Yeni Hesap";
        UiKit.StylePage(this);
        _kind.ItemsSource = new[] { "Firma sahibi / yönetici", "Mevcut firmaya personel" };
        _kind.SelectedIndex = 1;
        _kind.SelectedIndexChanged += (_, _) => UpdateMode();

        _ownerFields.Children.Add(_companyName);
        _ownerFields.Children.Add(_companyCodeOwner);
        _ownerFields.Children.Add(_branchName);
        _ownerFields.Children.Add(_branchCodeOwner);
        _ownerFields.Children.Add(_branchCity);
        _ownerFields.Children.Add(_branchAddress);

        var loadCompany = UiKit.SecondaryButton("Firmayı ve Şubeleri Bul");
        loadCompany.Clicked += async (_, _) => await LoadCompanyAsync(loadCompany);
        _staffFields.Children.Add(_inviteCode);
        _staffFields.Children.Add(UiKit.Label("Davet kodunuz varsa firma/şube kodu girmeniz gerekmez.", 10.5, false, true));
        _staffFields.Children.Add(_companyCodeStaff);
        _staffFields.Children.Add(loadCompany);
        _staffFields.Children.Add(_companyInfo);
        _staffFields.Children.Add(_branchPicker);
        _staffFields.Children.Add(_employee);

        var save = UiKit.PrimaryButton("Hesabı Oluştur / Başvuru Gönder");
        save.Clicked += async (_, _) => await SaveAsync(save);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 20, 18, 32),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("VALE hesabı oluşturun", 27, true),
                    UiKit.Label("Yeni bir firma açabilir veya mevcut firmanızın şubesine personel olarak katılabilirsiniz.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { UiKit.Label("Hesap türü", 15, true), _kind, _ownerFields, _staffFields }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Hesap bilgileri", 15, true), _name, _email, _phone, _password, _repeat,
                            UiKit.Label("Parola en az 10 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermeli.", 10.5, false, true)
                        }
                    }),
                    save
                }
            }
        };
        UpdateMode();
    }

    private bool IsOwner => _kind.SelectedIndex == 0;

    private void UpdateMode()
    {
        _ownerFields.IsVisible = IsOwner;
        _staffFields.IsVisible = !IsOwner;
    }

    private async Task LoadCompanyAsync(Button button)
    {
        if (string.IsNullOrWhiteSpace(_companyCodeStaff.Text))
        {
            await DisplayAlertAsync("Firma kodu", "Firma kodunu girin veya davet kodu kullanın.", "Tamam");
            return;
        }
        try
        {
            button.IsEnabled = false;
            await _api.EnsureServerReadyAsync();
            var company = await _api.GetRegistrationCompanyAsync(_companyCodeStaff.Text);
            _branches = company.Branches;
            _branchPicker.ItemsSource = _branches.Select(x => $"{x.Name} • {x.Code} • {x.City}").ToList();
            _branchPicker.SelectedIndex = _branches.Count == 1 ? 0 : -1;
            _companyInfo.Text = $"{company.Name} bulundu • {_branches.Count} aktif şube";
        }
        catch (Exception ex)
        {
            _branches = Array.Empty<RegistrationBranchDto>();
            _branchPicker.ItemsSource = null;
            _companyInfo.Text = ex.Message;
            await DisplayAlertAsync("Firma bulunamadı", ex.Message, "Tamam");
        }
        finally { button.IsEnabled = true; }
    }

    private async Task SaveAsync(Button save)
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            await DisplayAlertAsync("Eksik bilgi", "Ad soyad, e-posta ve parola alanlarını doldurun.", "Tamam");
            return;
        }
        if (_password.Text != _repeat.Text)
        {
            await DisplayAlertAsync("Parola", "Parolalar aynı değil.", "Tamam");
            return;
        }

        RegistrationBranchDto? selectedBranch = null;
        if (!IsOwner && string.IsNullOrWhiteSpace(_inviteCode.Text))
        {
            if (_branchPicker.SelectedIndex < 0 || _branchPicker.SelectedIndex >= _branches.Count)
            {
                await DisplayAlertAsync("Şube", "Önce firmayı yükleyip katılacağınız şubeyi seçin veya davet kodu girin.", "Tamam");
                return;
            }
            selectedBranch = _branches[_branchPicker.SelectedIndex];
        }

        var request = new RegisterRequest(
            FullName: _name.Text.Trim(),
            Email: _email.Text.Trim(),
            Password: _password.Text,
            PhoneNumber: N(_phone.Text),
            BranchCode: IsOwner ? N(_branchCodeOwner.Text) : selectedBranch?.Code,
            EmployeeCode: IsOwner ? null : N(_employee.Text),
            Kind: IsOwner ? RegistrationKind.CompanyOwner : RegistrationKind.Staff,
            CompanyCode: IsOwner ? N(_companyCodeOwner.Text) : N(_companyCodeStaff.Text),
            CompanyName: IsOwner ? N(_companyName.Text) : null,
            BranchName: IsOwner ? N(_branchName.Text) : null,
            BranchCity: IsOwner ? N(_branchCity.Text) : null,
            BranchAddress: IsOwner ? N(_branchAddress.Text) : null,
            InviteCode: IsOwner ? null : N(_inviteCode.Text));

        try
        {
            save.IsEnabled = false;
            await _api.EnsureServerReadyAsync();
            var result = await _api.RegisterAsync(request);
            await DisplayAlertAsync(result.RequiresApproval ? "Başvuru gönderildi" : "Firma hesabı hazır", result.Message, "Tamam");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Hesap oluşturulamadı", ex.Message, "Tamam");
        }
        finally { save.IsEnabled = true; }
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
