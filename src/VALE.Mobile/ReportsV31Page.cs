using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ReportsV31Page : ContentPage
{
    private readonly ApiClient _api;
    private readonly DatePicker _from = new() { Format = "dd.MM.yyyy" };
    private readonly DatePicker _to = new() { Format = "dd.MM.yyyy" };
    private readonly Label _vehicles = UiKit.Label("—", 24, true);
    private readonly Label _delivered = UiKit.Label("—", 24, true);
    private readonly Label _revenue = UiKit.Label("—", 24, true);
    private readonly Label _average = UiKit.Label("—", 24, true);
    private readonly Label _status = UiKit.Label("Bir tarih aralığı seçip raporu yenileyin.", 11.5, false, true);
    private readonly VerticalStackLayout _daily = new() { Spacing = 7 };
    private ReportSummaryDto? _report;
    private bool _busy;

    public ReportsV31Page(ApiClient api)
    {
        _api = api;
        Title = "Raporlar";
        UiKit.StylePage(this);
        _to.Date = DateTime.Today;
        _from.Date = DateTime.Today.AddDays(-7);

        var load = UiKit.PrimaryButton("Raporu Yenile");
        load.Clicked += async (_, _) => await LoadAsync();
        var pdfView = UiKit.SecondaryButton("PDF Görüntüle");
        pdfView.Clicked += async (_, _) => await ExportAsync("pdf-view");
        var pdfShare = UiKit.SecondaryButton("PDF Paylaş");
        pdfShare.Clicked += async (_, _) => await ExportAsync("pdf-share");
        var excel = UiKit.SecondaryButton("Excel Paylaş");
        excel.Clicked += async (_, _) => await ExportAsync("xlsx");
        var csv = UiKit.SecondaryButton("CSV Paylaş");
        csv.Clicked += async (_, _) => await ExportAsync("csv");

        var dates = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        dates.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Başlangıç", 10.5, true, true), _from } }, 0, 0);
        dates.Add(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Bitiş", 10.5, true, true), _to } }, 1, 0);

        var metrics = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 9,
            RowSpacing = 9
        };
        metrics.Add(MetricCard("ARAÇ", _vehicles, "Seçili dönemde giriş"), 0, 0);
        metrics.Add(MetricCard("TESLİM", _delivered, "Tamamlanan araç"), 1, 0);
        metrics.Add(MetricCard("CİRO", _revenue, "Toplam tahsilat"), 0, 1);
        metrics.Add(MetricCard("ORTALAMA", _average, "Teslim başına"), 1, 1);

        var exports = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        exports.Add(pdfView, 0, 0); exports.Add(pdfShare, 1, 0);
        exports.Add(excel, 0, 1); exports.Add(csv, 1, 1);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 28), Spacing = 14,
                Children =
                {
                    UiKit.Label("Raporlar", 27, true),
                    UiKit.Label("Şube performansını görüntüleyin; raporu PDF, Excel veya Excel uyumlu UTF‑8 CSV olarak paylaşın.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { dates, load, _status } }),
                    metrics,
                    UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label("Dışa aktar", 17, true), exports } }),
                    UiKit.Label("Günlük özet", 18, true),
                    _daily
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_report is null) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        var from = new DateTimeOffset(_from.Date, TimeZoneInfo.Local.GetUtcOffset(_from.Date));
        var endDate = _to.Date.AddDays(1).AddTicks(-1);
        var to = new DateTimeOffset(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate));
        if (to <= from)
        {
            await DisplayAlertAsync("Tarih aralığı", "Bitiş tarihi başlangıç tarihinden önce olamaz.", "Tamam");
            return;
        }
        try
        {
            _busy = true;
            _status.Text = "Rapor hazırlanıyor…";
            _report = await _api.GetReportAsync(from, to);
            _vehicles.Text = _report.TotalVehicles.ToString();
            _delivered.Text = _report.DeliveredVehicles.ToString();
            _revenue.Text = $"{_report.Revenue:N0} TL";
            _average.Text = $"{_report.AverageRevenuePerDelivery:N0} TL";
            _status.Text = $"{_report.From.ToLocalTime():dd.MM.yyyy} – {_report.To.ToLocalTime():dd.MM.yyyy} • Aktif: {_report.ActiveVehicles} • İptal: {_report.CancelledVehicles}";
            RenderDaily();
        }
        catch (Exception ex)
        {
            _status.Text = "Rapor alınamadı.";
            await DisplayAlertAsync("Rapor alınamadı", ex.Message, "Tamam");
        }
        finally { _busy = false; }
    }

    private void RenderDaily()
    {
        _daily.Clear();
        if (_report is null || _report.Daily.Count == 0)
        {
            _daily.Add(UiKit.Card(UiKit.Label("Seçili tarih aralığında kayıt yok. Rapor yine de 0 değerleriyle kullanılabilir.", 12.5, false, true)));
            return;
        }
        foreach (var row in _report.Daily.OrderByDescending(x => x.Day))
        {
            _daily.Add(UiKit.Card(new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Children =
                {
                    new VerticalStackLayout { Spacing = 2, Children = { UiKit.Label(row.Day.ToString("dd.MM.yyyy"), 13.5, true), UiKit.Label($"{row.Vehicles} araç • {row.Delivered} teslim", 11, false, true) } },
                    new Label { Text = $"{row.Revenue:N0} TL", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
                }
            }, new Thickness(12), 14));
        }
    }

    private async Task ExportAsync(string format)
    {
        if (_report is null) await LoadAsync();
        if (_report is null) return;
        try
        {
            string path;
            switch (format)
            {
                case "pdf-view":
                    path = await ReportExportService.WritePdfAsync(_report);
                    await Launcher.Default.OpenAsync(new OpenFileRequest("VALE Raporu", new ReadOnlyFile(path)));
                    return;
                case "pdf-share": path = await ReportExportService.WritePdfAsync(_report); break;
                case "xlsx": path = await ReportExportService.WriteXlsxAsync(_report); break;
                default: path = await ReportExportService.WriteCsvAsync(_report); break;
            }
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "VALE Raporu",
                File = new ShareFile(path)
            });
        }
        catch (Exception ex) { await DisplayAlertAsync("Dışa aktarma", ex.Message, "Tamam"); }
    }

    private static View MetricCard(string title, Label value, string subtitle) =>
        UiKit.Card(new VerticalStackLayout { Spacing = 3, Children = { UiKit.Label(title, 10, true, true), value, UiKit.Label(subtitle, 10.5, false, true) } }, new Thickness(12), 15);
}
