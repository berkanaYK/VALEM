using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ModernMainTabsPage : TabbedPage
{
    public ModernMainTabsPage(ApiClient api, UserDto user)
    {
        Title = "VALE";
        SetDynamicResource(BarBackgroundColorProperty, "ValeCard");
        SetDynamicResource(BarTextColorProperty, "ValeText");
        SelectedTabColor = ThemeService.Palette.Accent;
        UnselectedTabColor = ThemeService.Palette.Secondary;

        Children.Add(Tab("Ana Sayfa", new ModernDashboardPage(api, user)));
        Children.Add(Tab("Araçlar", new ModernTicketsPage(api)));
        Children.Add(Tab("Raporlar", new ModernReportsPage(api)));
        Children.Add(Tab("Profil", new ProfilePage(api, user)));
        Children.Add(Tab("Ayarlar", new SettingsPage(api)));
        if (user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            Children.Add(Tab("Yönetim", new AdminPage(api)));
        }
    }

    private static NavigationPage Tab(string title, Page page)
    {
        page.Title = title;
        var nav = new NavigationPage(page) { Title = title };
        nav.SetDynamicResource(NavigationPage.BarBackgroundColorProperty, "ValeCard");
        nav.SetDynamicResource(NavigationPage.BarTextColorProperty, "ValeText");
        return nav;
    }
}

public sealed class ModernDashboardPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly Label _active;
    private readonly Label _waiting;
    private readonly Label _delivered;
    private readonly Label _revenue;
    private readonly ObservableCollection<TicketSummaryDto> _recent = new();
    private bool _busy;

    public ModernDashboardPage(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;
        UiKit.StylePage(this);

        var a = UiKit.Metric("AKTİF", "—", "Şu an içeride"); _active = a.Value;
        var w = UiKit.Metric("İSTENEN", "—", "Teslim bekliyor"); _waiting = w.Value;
        var d = UiKit.Metric("TESLİM", "—", "Bugün tamamlandı"); _delivered = d.Value;
        var r = UiKit.Metric("CİRO", "—", "Bugünkü tahsilat"); _revenue = r.Value;
        var metrics = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        metrics.Add(a.Card, 0, 0); metrics.Add(w.Card, 1, 0); metrics.Add(d.Card, 0, 1); metrics.Add(r.Card, 1, 1);

        var add = UiKit.PrimaryButton("+ Yeni Araç Kabulü");
        add.Clicked += async (_, _) => await Navigation.PushAsync(new ModernNewTicketPage(_api));
        var reports = UiKit.SecondaryButton("Raporları Görüntüle");
        reports.Clicked += async (_, _) => await Navigation.PushAsync(new ModernReportsPage(_api));

        var list = new CollectionView
        {
            ItemsSource = _recent,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = ModernTicketTemplates.Card(),
            HeightRequest = 330,
            EmptyView = UiKit.Label("Aktif araç bulunmuyor.", 13, false, true)
        };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is TicketSummaryDto ticket)
            {
                list.SelectedItem = null;
                await Navigation.PushAsync(new ModernTicketDetailPage(_api, ticket.Id));
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 16,
                Children =
                {
                    UiKit.Label($"Günaydın, {_user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? _user.FullName}", 26, true),
                    UiKit.Label(_user.BranchName ?? "Şube", 13, false, true),
                    metrics,
                    add,
                    reports,
                    UiKit.Label("Son araçlar", 19, true),
                    list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_busy) return;
        try
        {
            _busy = true;
            var data = await _api.GetDashboardAsync();
            _active.Text = data.ActiveVehicles.ToString(CultureInfo.CurrentCulture);
            _waiting.Text = data.WaitingForDelivery.ToString(CultureInfo.CurrentCulture);
            _delivered.Text = data.DeliveredToday.ToString(CultureInfo.CurrentCulture);
            _revenue.Text = $"{data.RevenueToday:N0} TL";
            _recent.Clear();
            foreach (var item in data.RecentTickets) _recent.Add(item);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ana sayfa", ex.Message, "Tamam");
        }
        finally { _busy = false; }
    }
}

public sealed class ModernTicketsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<TicketSummaryDto> _items = new();
    private readonly Entry _search;
    private readonly Switch _closed;
    private readonly CollectionView _list;
    private bool _busy;

    public ModernTicketsPage(ApiClient api)
    {
        _api = api;
        UiKit.StylePage(this);
        _search = UiKit.Entry("Plaka, fiş veya telefon ara");
        _search.Completed += async (_, _) => await RefreshAsync();
        _closed = new Switch { OnColor = ThemeService.Palette.Accent };
        _closed.Toggled += async (_, _) => await RefreshAsync();
        var search = UiKit.SecondaryButton("Ara");
        search.Clicked += async (_, _) => await RefreshAsync();
        var add = UiKit.PrimaryButton("+ Yeni Araç");
        add.Clicked += async (_, _) => await Navigation.PushAsync(new ModernNewTicketPage(_api));

        _list = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = ModernTicketTemplates.Card(),
            EmptyView = UiKit.Label("Kayıt bulunamadı.", 13, false, true)
        };
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is TicketSummaryDto ticket)
            {
                _list.SelectedItem = null;
                await Navigation.PushAsync(new ModernTicketDetailPage(_api, ticket.Id));
            }
        };

        var searchRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        searchRow.Add(_search, 0, 0); searchRow.Add(search, 1, 0);
        Content = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)
            }
        };
        var root = (Grid)Content;
        root.Add(UiKit.Label("Araç yönetimi", 26, true), 0, 0);
        root.Add(searchRow, 0, 1);
        root.Add(new HorizontalStackLayout { Spacing = 9, Children = { _closed, UiKit.Label("Kapanan kayıtları göster", 12, false, true) } }, 0, 2);
        root.Add(add, 0, 3);
        root.Add(_list, 0, 4);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        try
        {
            _busy = true;
            var page = await _api.GetTicketsAsync(_search.Text, _closed.IsToggled);
            _items.Clear();
            foreach (var item in page.Items) _items.Add(item);
        }
        catch (Exception ex) { await DisplayAlertAsync("Araçlar", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }
}

public sealed class ModernNewTicketPage : ContentPage
{
    private const int MaxPhotoBytes = 4_000_000;
    private readonly ApiClient _api;
    private readonly Entry _plate = UiKit.Entry("07 ABC 123");
    private readonly Picker _brand = UiKit.Picker("Marka seçin");
    private readonly Picker _model = UiKit.Picker("Model seçin");
    private readonly Entry _manualBrand = UiKit.Entry("Marka (elle)");
    private readonly Entry _manualModel = UiKit.Entry("Model (elle)");
    private readonly Entry _year = UiKit.Entry("Model yılı", Keyboard.Numeric);
    private readonly Entry _color = UiKit.Entry("Renk");
    private readonly Picker _fuel = UiKit.Picker("Yakıt tipi");
    private readonly Picker _transmission = UiKit.Picker("Şanzıman");
    private readonly Entry _customer = UiKit.Entry("Müşteri adı");
    private readonly Entry _phone = UiKit.Entry("Telefon", Keyboard.Telephone);
    private readonly Entry _key = UiKit.Entry("Anahtar etiketi");
    private readonly Entry _spot = UiKit.Entry("Park yeri");
    private readonly Editor _notes = UiKit.Editor("Notlar / araçtaki mevcut hasarlar");
    private readonly Entry _rate = UiKit.Entry("Saatlik ücret (boş = varsayılan)", Keyboard.Numeric);
    private readonly Image _photo = new() { HeightRequest = 190, Aspect = Aspect.AspectFill, IsVisible = false };
    private readonly Label _photoInfo = UiKit.Label("Fotoğraf eklenmedi", 11, false, true);
    private string? _photoBase64;

    public ModernNewTicketPage(ApiClient api)
    {
        _api = api;
        Title = "Yeni Araç";
        UiKit.StylePage(this);
        _brand.ItemsSource = VehicleCatalog.Brands.Select(x => x.Name).Concat(new[] { "Diğer" }).ToList();
        _fuel.ItemsSource = VehicleCatalog.FuelTypes.ToList();
        _transmission.ItemsSource = VehicleCatalog.Transmissions.ToList();
        _manualBrand.IsVisible = false;
        _manualModel.IsVisible = false;
        _brand.SelectedIndexChanged += (_, _) => UpdateModels();
        _year.Text = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);

        var pickPhoto = UiKit.SecondaryButton("Fotoğraf Seç");
        pickPhoto.Clicked += async (_, _) => await PickPhotoAsync();
        var removePhoto = UiKit.TextButton("Fotoğrafı Kaldır");
        removePhoto.Clicked += (_, _) =>
        {
            _photoBase64 = null;
            _photo.Source = null;
            _photo.IsVisible = false;
            _photoInfo.Text = "Fotoğraf eklenmedi";
        };
        var save = UiKit.PrimaryButton("Araç Kabulünü Kaydet");
        save.Clicked += async (_, _) => await SaveAsync(save);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    UiKit.Label("Araç kabul", 27, true),
                    UiKit.Label("Plaka ve araç bilgilerini girin; isterseniz fotoğraf ekleyin. Hazır marka/model listesi elle girişi engellemez.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Araç", 16, true), _plate, _brand, _model, _manualBrand, _manualModel,
                            _year, _color, _fuel, _transmission
                        }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { UiKit.Label("Araç fotoğrafı", 16, true), _photo, _photoInfo, pickPhoto, removePhoto }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { UiKit.Label("Müşteri ve operasyon", 16, true), _customer, _phone, _key, _spot, _notes, _rate }
                    }),
                    save
                }
            }
        };
    }

    private void UpdateModels()
    {
        var brand = _brand.SelectedItem?.ToString();
        var manual = brand == "Diğer";
        _manualBrand.IsVisible = manual;
        _manualModel.IsVisible = manual;
        _model.IsVisible = !manual;
        _model.ItemsSource = manual ? null : VehicleCatalog.ModelsFor(brand).ToList();
        _model.SelectedIndex = -1;
    }

    private async Task PickPhotoAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Araç fotoğrafını seçin",
                FileTypes = FilePickerFileType.Images
            });
            if (result is null) return;
            await using var input = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            if (memory.Length > MaxPhotoBytes)
            {
                await DisplayAlertAsync("Fotoğraf çok büyük", "Fotoğraf en fazla 4 MB olabilir. Daha küçük bir fotoğraf seçin.", "Tamam");
                return;
            }
            var bytes = memory.ToArray();
            _photoBase64 = Convert.ToBase64String(bytes);
            _photo.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _photo.IsVisible = true;
            _photoInfo.Text = $"{result.FileName} • {bytes.Length / 1024d:N0} KB";
        }
        catch (Exception ex) { await DisplayAlertAsync("Fotoğraf", ex.Message, "Tamam"); }
    }

    private async Task SaveAsync(Button save)
    {
        var plate = (_plate.Text ?? "").Trim().ToUpperInvariant();
        if (plate.Length < 3)
        {
            await DisplayAlertAsync("Plaka", "Geçerli bir plaka girin.", "Tamam");
            return;
        }

        int? year = null;
        if (!string.IsNullOrWhiteSpace(_year.Text))
        {
            if (!int.TryParse(_year.Text, out var parsed) || parsed is < 1900 or > 2100)
            {
                await DisplayAlertAsync("Model yılı", "1900-2100 arasında geçerli bir yıl girin.", "Tamam");
                return;
            }
            year = parsed;
        }
        decimal? rate = null;
        if (!string.IsNullOrWhiteSpace(_rate.Text) && !decimal.TryParse(_rate.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedRate))
        {
            await DisplayAlertAsync("Ücret", "Saatlik ücret geçerli değil.", "Tamam");
            return;
        }
        else if (!string.IsNullOrWhiteSpace(_rate.Text)) rate = parsedRate;

        var manual = _brand.SelectedItem?.ToString() == "Diğer";
        var brand = manual ? N(_manualBrand.Text) : N(_brand.SelectedItem?.ToString());
        var model = manual ? N(_manualModel.Text) : N(_model.SelectedItem?.ToString());
        try
        {
            save.IsEnabled = false;
            await _api.CreateTicketAsync(new CreateTicketRequest(
                null, plate, brand, model, N(_color.Text), N(_customer.Text), N(_phone.Text), N(_key.Text), N(_spot.Text),
                N(_notes.Text), rate, year, N(_fuel.SelectedItem?.ToString()), N(_transmission.SelectedItem?.ToString()), _photoBase64));
            await DisplayAlertAsync("Kaydedildi", "Araç başarıyla teslim alındı.", "Tamam");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Kayıt başarısız", ex.Message, "Tamam"); }
        finally { save.IsEnabled = true; }
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ModernTicketDetailPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Guid _id;
    private readonly VerticalStackLayout _body = new() { Spacing = 12 };
    private readonly VerticalStackLayout _actions = new() { Spacing = 9 };
    private bool _busy;

    public ModernTicketDetailPage(ApiClient api, Guid id)
    {
        _api = api;
        _id = id;
        Title = "Araç Detayı";
        UiKit.StylePage(this);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children = { _body, _actions }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        try
        {
            _busy = true;
            var detail = await _api.GetTicketDetailAsync(_id);
            Render(detail);
        }
        catch (Exception ex) { await DisplayAlertAsync("Araç detayı", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }

    private void Render(TicketDetailDto detail)
    {
        var t = detail.Ticket;
        _body.Clear();
        _actions.Clear();
        _body.Add(UiKit.Label(t.LicensePlate, 30, true));
        _body.Add(UiKit.Label(StatusText(t.Status), 13, true, true));
        if (!string.IsNullOrWhiteSpace(detail.PhotoBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(detail.PhotoBase64);
                _body.Add(UiKit.Card(new Image
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
                    HeightRequest = 230,
                    Aspect = Aspect.AspectFill
                }, new Thickness(0), 20));
            }
            catch { }
        }

        var vehicle = new VerticalStackLayout { Spacing = 7 };
        vehicle.Add(UiKit.Label("Araç bilgileri", 17, true));
        AddDetail(vehicle, "Marka / model", t.VehicleDescription);
        AddDetail(vehicle, "Model yılı", t.Year?.ToString() ?? "—");
        AddDetail(vehicle, "Yakıt", t.FuelType ?? "—");
        AddDetail(vehicle, "Şanzıman", t.Transmission ?? "—");
        AddDetail(vehicle, "Park yeri", t.ParkingSpot ?? "—");
        AddDetail(vehicle, "Anahtar", t.KeyTag ?? "—");
        _body.Add(UiKit.Card(vehicle));

        var operation = new VerticalStackLayout { Spacing = 7 };
        operation.Add(UiKit.Label("Operasyon", 17, true));
        AddDetail(operation, "Fiş", t.TicketNumber);
        AddDetail(operation, "Giriş", t.EntryAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        AddDetail(operation, "Müşteri", t.CustomerName ?? "—");
        AddDetail(operation, "Telefon", t.CustomerPhone ?? "—");
        AddDetail(operation, "Tutar", $"{t.AmountDue:N2} TL");
        if (!string.IsNullOrWhiteSpace(t.Notes)) AddDetail(operation, "Not", t.Notes);
        _body.Add(UiKit.Card(operation));

        if (t.Status == TicketStatus.Received) AddAction("Park Edildi", () => ChangeStatus(TicketStatus.Parked));
        if (t.Status == TicketStatus.Parked) AddAction("Araç İsteniyor", () => ChangeStatus(TicketStatus.Requested));
        if (t.Status == TicketStatus.Requested)
        {
            AddAction("Nakit ile Teslim Et", () => Checkout(PaymentMethod.Cash));
            AddAction("Kart ile Teslim Et", () => Checkout(PaymentMethod.Card));
            AddAction("Havale / EFT ile Teslim Et", () => Checkout(PaymentMethod.Transfer));
        }
        if (t.Status is TicketStatus.Received or TicketStatus.Parked)
        {
            var cancel = UiKit.TextButton("Kaydı İptal Et");
            cancel.TextColor = ThemeService.Palette.Danger;
            cancel.Clicked += async (_, _) =>
            {
                var ok = await DisplayAlertAsync("Kaydı iptal et", "Bu vale kaydı iptal edilsin mi?", "İptal Et", "Vazgeç");
                if (ok) await ChangeStatus(TicketStatus.Cancelled);
            };
            _actions.Add(cancel);
        }
    }

    private void AddAction(string text, Func<Task> action)
    {
        var button = UiKit.PrimaryButton(text);
        button.Clicked += async (_, _) => await action();
        _actions.Add(button);
    }

    private async Task ChangeStatus(TicketStatus status)
    {
        try { await _api.UpdateStatusAsync(_id, status); await LoadAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("İşlem", ex.Message, "Tamam"); }
    }

    private async Task Checkout(PaymentMethod method)
    {
        var ok = await DisplayAlertAsync("Teslimi onayla", "Araç teslim edilip tahsilat kaydedilsin mi?", "Teslim Et", "Vazgeç");
        if (!ok) return;
        try
        {
            var result = await _api.CheckoutAsync(_id, method);
            await DisplayAlertAsync("Teslim tamamlandı", $"Tahsilat: {result.PaidAmount:N2} TL", "Tamam");
            await LoadAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Teslim", ex.Message, "Tamam"); }
    }

    private static void AddDetail(VerticalStackLayout layout, string name, string value)
    {
        layout.Add(UiKit.Label(name, 10.5, true, true));
        layout.Add(UiKit.Label(value, 14));
    }

    private static string StatusText(TicketStatus status) => status switch
    {
        TicketStatus.Received => "Teslim alındı",
        TicketStatus.Parked => "Park edildi",
        TicketStatus.Requested => "Araç isteniyor",
        TicketStatus.Delivered => "Teslim edildi",
        TicketStatus.Cancelled => "İptal edildi",
        _ => status.ToString()
    };
}

public sealed class ModernReportsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly DatePicker _from = new();
    private readonly DatePicker _to = new();
    private readonly Picker _preset = UiKit.Picker("Dönem seçin");
    private readonly Label _vehicles;
    private readonly Label _delivered;
    private readonly Label _revenue;
    private readonly Label _average;
    private readonly Label _parking;
    private readonly Label _cancelled;
    private readonly Label _payments = UiKit.Label("—", 12.5, false, true);
    private readonly Label _brands = UiKit.Label("—", 12.5, false, true);
    private readonly ObservableCollection<DailyReportPointDto> _daily = new();
    private readonly DailyRevenueDrawable _dailyDrawable = new();
    private readonly HourlyDeliveryDrawable _hourlyDrawable = new();
    private readonly GraphicsView _dailyChart;
    private readonly GraphicsView _hourlyChart;
    private ReportSummaryDto? _lastReport;
    private bool _loading;

    public ModernReportsPage(ApiClient api)
    {
        _api = api;
        Title = "Raporlar";
        UiKit.StylePage(this);
        _from.Date = DateTime.Today.AddDays(-6);
        _to.Date = DateTime.Today;
        _preset.ItemsSource = new[] { "Bugün", "Son 7 Gün", "Son 30 Gün", "Son 90 Gün", "Özel Tarih" };
        _preset.SelectedIndex = 1;
        _preset.SelectedIndexChanged += async (_, _) =>
        {
            ApplyPreset();
            if (!_loading) await LoadAsync();
        };

        var m1 = UiKit.Metric("ARAÇ", "—", "Giriş yapan"); _vehicles = m1.Value;
        var m2 = UiKit.Metric("TESLİM", "—", "Tamamlanan"); _delivered = m2.Value;
        var m3 = UiKit.Metric("CİRO", "—", "Tahsilat"); _revenue = m3.Value;
        var m4 = UiKit.Metric("ORT. TUTAR", "—", "Teslim başına"); _average = m4.Value;
        var m5 = UiKit.Metric("ORT. SÜRE", "—", "Park süresi"); _parking = m5.Value;
        var m6 = UiKit.Metric("İPTAL", "—", "İptal edilen"); _cancelled = m6.Value;
        var metrics = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 9, RowSpacing = 9
        };
        metrics.Add(m1.Card, 0, 0); metrics.Add(m2.Card, 1, 0); metrics.Add(m3.Card, 0, 1);
        metrics.Add(m4.Card, 1, 1); metrics.Add(m5.Card, 0, 2); metrics.Add(m6.Card, 1, 2);

        _dailyChart = new GraphicsView { Drawable = _dailyDrawable, HeightRequest = 220 };
        _hourlyChart = new GraphicsView { Drawable = _hourlyDrawable, HeightRequest = 190 };
        var load = UiKit.PrimaryButton("Raporu Güncelle");
        load.Clicked += async (_, _) => await LoadAsync();
        var export = UiKit.SecondaryButton("CSV Olarak Paylaş");
        export.Clicked += async (_, _) => await ShareCsvAsync();

        var dates = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
        dates.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Başlangıç", 10.5, true, true), _from } }, 0, 0);
        dates.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Bitiş", 10.5, true, true), _to } }, 1, 0);

        var dailyList = new CollectionView
        {
            ItemsSource = _daily,
            SelectionMode = SelectionMode.None,
            HeightRequest = 330,
            ItemTemplate = new DataTemplate(() =>
            {
                var day = UiKit.Label("", 13, true);
                day.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Day), stringFormat: "{0:dd.MM.yyyy}"));
                var cars = UiKit.Label("", 11.5, false, true);
                cars.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Vehicles), stringFormat: "Araç: {0}"));
                var delivered = UiKit.Label("", 11.5, false, true);
                delivered.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Delivered), stringFormat: "Teslim: {0}"));
                var revenue = UiKit.Label("", 12, true);
                revenue.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Revenue), stringFormat: "{0:N2} TL"));
                var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                grid.Add(new VerticalStackLayout { Spacing = 2, Children = { day, cars, delivered } }, 0, 0);
                grid.Add(revenue, 1, 0);
                return UiKit.Card(grid, new Thickness(12), 14);
            })
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    UiKit.Label("Raporlar ve analiz", 27, true),
                    UiKit.Label("Tarih aralığını seçerek operasyon, tahsilat ve yoğunluk trendlerini inceleyin.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { _preset, dates, load } }),
                    metrics,
                    UiKit.Label("Günlük ciro grafiği", 18, true),
                    UiKit.Card(_dailyChart),
                    UiKit.Label("Saatlik teslim yoğunluğu", 18, true),
                    UiKit.Card(_hourlyChart),
                    UiKit.Label("Ödeme dağılımı", 18, true),
                    UiKit.Card(_payments),
                    UiKit.Label("En sık gelen markalar", 18, true),
                    UiKit.Card(_brands),
                    export,
                    UiKit.Label("Günlük detay", 18, true),
                    dailyList
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_lastReport is null) await LoadAsync();
    }

    private void ApplyPreset()
    {
        var today = DateTime.Today;
        switch (_preset.SelectedIndex)
        {
            case 0: _from.Date = today; _to.Date = today; break;
            case 1: _from.Date = today.AddDays(-6); _to.Date = today; break;
            case 2: _from.Date = today.AddDays(-29); _to.Date = today; break;
            case 3: _from.Date = today.AddDays(-89); _to.Date = today; break;
        }
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        try
        {
            _loading = true;
            var fromDate = _from.Date ?? DateTime.Today.AddDays(-6);
            var toDate = _to.Date ?? DateTime.Today;
            if (toDate < fromDate)
            {
                await DisplayAlertAsync("Tarih", "Bitiş tarihi başlangıç tarihinden önce olamaz.", "Tamam");
                return;
            }
            var from = LocalStart(fromDate);
            var to = LocalStart(toDate.AddDays(1)).AddTicks(-1);
            var report = await _api.GetReportAsync(from, to);
            _lastReport = report;
            _vehicles.Text = report.TotalVehicles.ToString(CultureInfo.CurrentCulture);
            _delivered.Text = report.DeliveredVehicles.ToString(CultureInfo.CurrentCulture);
            _revenue.Text = $"{report.Revenue:N0} TL";
            _average.Text = $"{report.AverageRevenuePerDelivery:N0} TL";
            _parking.Text = FormatDuration(report.AverageParkingMinutes);
            _cancelled.Text = report.CancelledVehicles.ToString(CultureInfo.CurrentCulture);
            _payments.Text = string.Join("\n", report.Payments.Select(x => $"{PaymentText(x.Method)}  •  {x.Count} işlem  •  {x.Amount:N2} TL"));
            _brands.Text = report.TopBrands.Count == 0 ? "Veri yok" : string.Join("\n", report.TopBrands.Select((x, i) => $"{i + 1}. {x.Brand}  •  {x.Vehicles} araç"));
            _daily.Clear();
            foreach (var point in report.Daily) _daily.Add(point);
            _dailyDrawable.Points = report.Daily;
            _hourlyDrawable.Points = report.Hourly;
            _dailyChart.Invalidate();
            _hourlyChart.Invalidate();
        }
        catch (Exception ex) { await DisplayAlertAsync("Rapor alınamadı", ex.Message, "Tamam"); }
        finally { _loading = false; }
    }

    private async Task ShareCsvAsync()
    {
        if (_lastReport is null) { await LoadAsync(); if (_lastReport is null) return; }
        var report = _lastReport;
        var sb = new StringBuilder();
        sb.AppendLine("Tarih;Araç;Teslim;Ciro");
        foreach (var day in report.Daily) sb.AppendLine($"{day.Day:dd.MM.yyyy};{day.Vehicles};{day.Delivered};{day.Revenue.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine($"Toplam Araç;{report.TotalVehicles}");
        sb.AppendLine($"Teslim;{report.DeliveredVehicles}");
        sb.AppendLine($"İptal;{report.CancelledVehicles}");
        sb.AppendLine($"Ciro;{report.Revenue.ToString(CultureInfo.InvariantCulture)}");
        var path = Path.Combine(FileSystem.CacheDirectory, $"VALE-Rapor-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        await Share.Default.RequestAsync(new ShareFileRequest("VALE raporu", new ShareFile(path)));
    }

    private static DateTimeOffset LocalStart(DateTime date)
    {
        var unspecified = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    private static string FormatDuration(double minutes) => minutes < 60 ? $"{minutes:N0} dk" : $"{minutes / 60:N1} sa";
    private static string PaymentText(PaymentMethod method) => method switch { PaymentMethod.Cash => "Nakit", PaymentMethod.Card => "Kart", _ => "Havale / EFT" };
}

public static class ModernTicketTemplates
{
    public static DataTemplate Card() => new(() =>
    {
        var plate = UiKit.Label("", 18, true);
        plate.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.LicensePlate));
        var description = UiKit.Label("", 12, false, true);
        description.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.VehicleDescription));
        var status = UiKit.Label("", 12, true);
        status.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.Status), converter: new ModernTicketStatusConverter()));
        var time = UiKit.Label("", 11, false, true);
        time.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.EntryAt), stringFormat: "Giriş: {0:dd.MM HH:mm}"));
        var photo = UiKit.Label("Fotoğraflı kayıt", 10.5, true, true);
        photo.SetBinding(VisualElement.IsVisibleProperty, nameof(TicketSummaryDto.HasPhoto));
        var right = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End, Children = { status, photo } };
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { plate, description, time } }, 0, 0);
        grid.Add(right, 1, 0);
        return UiKit.Card(grid, new Thickness(13), 16);
    });
}

public sealed class ModernTicketStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is TicketStatus status
        ? status switch
        {
            TicketStatus.Received => "Teslim alındı",
            TicketStatus.Parked => "Park edildi",
            TicketStatus.Requested => "Araç isteniyor",
            TicketStatus.Delivered => "Teslim edildi",
            TicketStatus.Cancelled => "İptal",
            _ => status.ToString()
        }
        : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
