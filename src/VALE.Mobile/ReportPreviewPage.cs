using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ReportPreviewPage : ContentPage
{
    public ReportPreviewPage(ReportSummaryDto report, string path, string format)
    {
        Title = format == "xlsx-view" ? "Excel Önizleme" : "CSV Önizleme";
        UiKit.StylePage(this);
        var body = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        body.Add(UiKit.Label(Title, 25, true));
        body.Add(UiKit.Label($"{report.TotalVehicles} araç • {report.DeliveredVehicles} teslim • {report.Revenue:N2} TL", 14, true));
        var table = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 12, RowSpacing = 12 };
        var headers = new[] { "Tarih", "Araç", "Teslim", "Ciro (TL)" };
        for (var col = 0; col < 4; col++) table.Add(UiKit.Label(headers[col], 12, true), col, 0);
        var row = 1;
        foreach (var day in report.Daily)
        {
            var values = new[] { day.Day.ToString("dd.MM.yyyy"), day.Vehicles.ToString(), day.Delivered.ToString(), day.Revenue.ToString("N2") };
            for (var col = 0; col < 4; col++) table.Add(UiKit.Label(values[col], 12), col, row);
            row++;
        }
        body.Add(UiKit.Card(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = table }));
        var open = UiKit.SecondaryButton("Dosyayı Başka Uygulamada Aç");
        open.Clicked += async (_, _) =>
        {
            try { await Launcher.Default.OpenAsync(new OpenFileRequest(Title, new ReadOnlyFile(path))); }
            catch { await DisplayAlertAsync("Dosya açılamadı", "Bu dosya türünü açabilen bir uygulama yükleyebilir veya raporu paylaşabilirsiniz.", "Tamam"); }
        };
        body.Add(open); Content = new ScrollView { Content = body };
    }
}
