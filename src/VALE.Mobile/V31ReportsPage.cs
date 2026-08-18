using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class V31ReportsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly DatePicker _from = new();
    private readonly DatePicker _to = new();
    private readonly Picker _preset = UiKit.Picker("Dönem seçin");
    private readonly Label _vehicles = UiKit.Label("—", 22, true);
    private readonly Label _delivered = UiKit.Label("—", 22, true);
    private readonly Label _revenue = UiKit.Label("—", 22, true);
    private readonly Label _average = UiKit.Label("—", 22, true);
    private readonly Label _status = UiKit.Label("Raporu oluşturmak için tarih aralığını seçin.", 11.5, false, true);
    private readonly ObservableCollection<DailyReportPointDto> _daily = new();
    private readonly Button _pdfOpen;
    private readonly Button _pdfShare;
    private readonly Button _excelShare;
    private readonly Button _csvShare;
    private ReportSummaryDto? _report;
    private bool _busy;

    public V31ReportsPage(ApiClient api)
    {
        _api = api;
        Title = "Raporlar";
        UiKit.StylePage(this);
        _from.Date = DateTime.Today.AddDays(-7);
        _to.Date = DateTime.Today;
        _preset.ItemsSource = new[] { "Son 7 gün", "Bugün", "Bu hafta", "Bu ay", "Son 30 gün" };
        _preset.SelectedIndex = 0;
        _preset.SelectedIndexChanged += (_, _) => ApplyPreset();

        var load = UiKit.PrimaryButton("Raporu Oluştur");
        load.Clicked += async (_, _) => await LoadAsync(load);
        _pdfOpen = UiKit.SecondaryButton("PDF Görüntüle");
        _pdfShare = UiKit.SecondaryButton("PDF Paylaş");
        _excelShare = UiKit.SecondaryButton("Excel Paylaş");
        _csvShare = UiKit.SecondaryButton("CSV Paylaş");
        SetExportEnabled(false);
        _pdfOpen.Clicked += async (_, _) => await ExportAsync("pdf-open");
        _pdfShare.Clicked += async (_, _) => await ExportAsync("pdf-share");
        _excelShare.Clicked += async (_, _) => await ExportAsync("xlsx");
        _csvShare.Clicked += async (_, _) => await ExportAsync("csv");

        var dateGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        dateGrid.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Başlangıç", 10.5, true, true), _from } }, 0, 0);
        dateGrid.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Bitiş", 10.5, true, true), _to } }, 1, 0);

        var metrics = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 8, RowSpacing = 8
        };
        metrics.Add(Metric("ARAÇ", _vehicles), 0, 0);
        metrics.Add(Metric("TESLİM", _delivered), 1, 0);
        metrics.Add(Metric("CİRO", _revenue), 0, 1);
        metrics.Add(Metric("ORT. SÜRE", _average), 1, 1);

        var dailyList = new CollectionView
        {
            ItemsSource = _daily,
            HeightRequest = 300,
            SelectionMode = SelectionMode.None,
            EmptyView = UiKit.Label("Seçilen dönemde günlük veri yok.", 12, false, true),
            ItemTemplate = new DataTemplate(() =>
            {
                var date = UiKit.Label("", 13, true); date.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Day), stringFormat: "{0:dd.MM.yyyy}"));
                var count = UiKit.Label("", 11.5, false, true); count.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Vehicles), stringFormat: "Araç: {0}"));
                var delivered = UiKit.Label("", 11.5, false, true); delivered.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Delivered), stringFormat: "Teslim: {0}"));
                var revenue = UiKit.Label("", 11.5, false, true); revenue.SetBinding(Label.TextProperty, new Binding(nameof(DailyReportPointDto.Revenue), stringFormat: "Ciro: {0:N2} TL"));
                return UiKit.Card(new VerticalStackLayout { Spacing = 3, Children = { date, new HorizontalStackLayout { Spacing = 12, Children = { count, delivered, revenue } } } }, new Thickness(10), 14);
            })
        };

        var exportGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 8, RowSpacing = 8
        };
        exportGrid.Add(_pdfOpen, 0, 0); exportGrid.Add(_pdfShare, 1, 0);
        exportGrid.Add(_excelShare, 0, 1); exportGrid.Add(_csvShare, 1, 1);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 13,
                Children =
                {
                    UiKit.Label("Raporlama", 27, true),
                    UiKit.Label("Boş dönemler hata vermez; sonuç sıfır değerlerle gösterilir. Dosyalar Türkçe karakter uyumlu oluşturulur.", 11.5, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { _preset, dateGrid, load, _status } }),
                    metrics,
                    UiKit.Label("Günlük özet", 18, true), dailyList,
                    UiKit.Label("Dışa aktar", 18, true), exportGrid
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_report is null) await LoadAsync(null);
    }

    private void ApplyPreset()
    {
        var today = DateTime.Today;
        switch (_preset.SelectedIndex)
        {
            case 1: _from.Date = today; _to.Date = today; break;
            case 2:
                var diff = ((int)today.DayOfWeek + 6) % 7;
                _from.Date = today.AddDays(-diff); _to.Date = today; break;
            case 3: _from.Date = new DateTime(today.Year, today.Month, 1); _to.Date = today; break;
            case 4: _from.Date = today.AddDays(-30); _to.Date = today; break;
            default: _from.Date = today.AddDays(-7); _to.Date = today; break;
        }
    }

    private async Task LoadAsync(Button? button)
    {
        if (_busy) return;
        if (_to.Date < _from.Date)
        {
            await DisplayAlertAsync("Tarih", "Bitiş tarihi başlangıç tarihinden önce olamaz.", "Tamam");
            return;
        }
        try
        {
            _busy = true;
            if (button is not null) button.IsEnabled = false;
            _status.Text = "Rapor hazırlanıyor…";
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            var from = new DateTimeOffset(_from.Date, offset);
            var to = new DateTimeOffset(_to.Date.AddDays(1).AddTicks(-1), offset);
            _report = await _api.GetReportAsync(from, to);
            _vehicles.Text = _report.TotalVehicles.ToString(CultureInfo.CurrentCulture);
            _delivered.Text = _report.DeliveredVehicles.ToString(CultureInfo.CurrentCulture);
            _revenue.Text = $"{_report.Revenue:N2} TL";
            _average.Text = $"{_report.AverageParkingMinutes:N0} dk";
            _daily.Clear(); foreach (var item in _report.Daily) _daily.Add(item);
            _status.Text = _report.TotalVehicles == 0 && _report.Revenue == 0 ? "Bu dönemde kayıt yok; rapor sıfır değerlerle hazırlandı." : "Rapor hazır.";
            SetExportEnabled(true);
        }
        catch (Exception ex)
        {
            _report = null; SetExportEnabled(false); _status.Text = ex.Message;
            await DisplayAlertAsync("Rapor alınamadı", ex.Message, "Tamam");
        }
        finally
        {
            _busy = false;
            if (button is not null) button.IsEnabled = true;
        }
    }

    private async Task ExportAsync(string kind)
    {
        if (_report is null) return;
        try
        {
            string path;
            switch (kind)
            {
                case "pdf-open":
                    path = await ReportExportService.CreatePdfAsync(_report);
                    if (!await ReportExportService.OpenAsync(path, "VALE Raporu")) await ReportExportService.ShareAsync(path, "VALE Raporu PDF");
                    break;
                case "pdf-share":
                    path = await ReportExportService.CreatePdfAsync(_report);
                    await ReportExportService.ShareAsync(path, "VALE Raporu PDF");
                    break;
                case "xlsx":
                    path = await ReportExportService.CreateExcelAsync(_report);
                    await ReportExportService.ShareAsync(path, "VALE Raporu Excel");
                    break;
                default:
                    path = await ReportExportService.CreateCsvAsync(_report);
                    await ReportExportService.ShareAsync(path, "VALE Raporu CSV");
                    break;
            }
        }
        catch (Exception ex) { await DisplayAlertAsync("Dışa aktarma", ex.Message, "Tamam"); }
    }

    private void SetExportEnabled(bool enabled)
    {
        _pdfOpen.IsEnabled = enabled; _pdfShare.IsEnabled = enabled; _excelShare.IsEnabled = enabled; _csvShare.IsEnabled = enabled;
    }

    private static View Metric(string title, Label value) => UiKit.Card(new VerticalStackLayout
    {
        Spacing = 3,
        Children = { UiKit.Label(title, 10, true, true), value }
    }, new Thickness(12), 16);
}
