using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Android.Graphics;
using Android.Graphics.Pdf;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public static class ReportExportService
{
    public static async Task<string> CreateCsvAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, FileName(report, "csv"));
        var sb = new StringBuilder();
        sb.AppendLine("Tarih;Araç;Teslim;Ciro (TL)");
        foreach (var row in report.Daily)
            sb.AppendLine($"{row.Day:dd.MM.yyyy};{row.Vehicles};{row.Delivered};{row.Revenue.ToString("0.00", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine($"Toplam Araç;{report.TotalVehicles}");
        sb.AppendLine($"Teslim;{report.DeliveredVehicles}");
        sb.AppendLine($"İptal;{report.CancelledVehicles}");
        sb.AppendLine($"Aktif;{report.ActiveVehicles}");
        sb.AppendLine($"Toplam Ciro;{report.Revenue.ToString("0.00", CultureInfo.InvariantCulture)}");
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public static async Task<string> CreateExcelAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, FileName(report, "xlsx"));
        if (File.Exists(path)) File.Delete(path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        WriteEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="VALE Rapor" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "VALE RAPORU", $"{report.From:dd.MM.yyyy}", $"{report.To:dd.MM.yyyy}" },
            new object?[] { "Toplam Araç", report.TotalVehicles, "Teslim", report.DeliveredVehicles, "İptal", report.CancelledVehicles },
            new object?[] { "Aktif", report.ActiveVehicles, "Ciro (TL)", report.Revenue, "Ort. Süre (dk)", Math.Round(report.AverageParkingMinutes, 1) },
            Array.Empty<object?>(),
            new object?[] { "Tarih", "Araç", "Teslim", "Ciro (TL)" }
        };
        rows.AddRange(report.Daily.Select(x => (IReadOnlyList<object?>)new object?[] { x.Day.ToString("dd.MM.yyyy"), x.Vehicles, x.Delivered, x.Revenue }));
        WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(rows));
        await Task.CompletedTask;
        return path;
    }

    public static async Task<string> CreatePdfAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, FileName(report, "pdf"));
        using var document = new PdfDocument();
        var pageNumber = 1;
        PdfDocument.Page? page = null;
        Canvas? canvas = null;
        var paint = new Paint(PaintFlags.AntiAlias) { Color = Color.Black, TextSize = 12f };
        var titlePaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Black, TextSize = 22f, FakeBoldText = true };
        float y = 0;

        void NewPage()
        {
            if (page is not null) document.FinishPage(page);
            var info = new PdfDocument.PageInfo.Builder(595, 842, pageNumber++).Create();
            page = document.StartPage(info);
            canvas = page.Canvas;
            y = 42;
            canvas.DrawText("VALE Raporu", 36, y, titlePaint); y += 26;
            canvas.DrawText($"{report.From:dd.MM.yyyy HH:mm} - {report.To:dd.MM.yyyy HH:mm}", 36, y, paint); y += 28;
        }

        void Line(string text, bool important = false)
        {
            if (canvas is null || page is null) NewPage();
            if (y > 800) NewPage();
            canvas!.DrawText(text, 36, y, important ? new Paint(paint) { FakeBoldText = true } : paint);
            y += 19;
        }

        NewPage();
        Line($"Toplam araç: {report.TotalVehicles}", true);
        Line($"Teslim edilen: {report.DeliveredVehicles}");
        Line($"İptal edilen: {report.CancelledVehicles}");
        Line($"Aktif araç: {report.ActiveVehicles}");
        Line($"Toplam ciro: {report.Revenue:N2} TL", true);
        Line($"Teslim başına ortalama: {report.AverageRevenuePerDelivery:N2} TL");
        Line($"Ortalama park süresi: {report.AverageParkingMinutes:N0} dakika");
        y += 12;
        Line("Günlük özet", true);
        foreach (var row in report.Daily)
            Line($"{row.Day:dd.MM.yyyy}   Araç: {row.Vehicles}   Teslim: {row.Delivered}   Ciro: {row.Revenue:N2} TL");
        y += 10;
        Line("Ödeme dağılımı", true);
        foreach (var row in report.Payments)
            Line($"{PaymentText(row.Method)}   İşlem: {row.Count}   Tutar: {row.Amount:N2} TL");
        if (report.TopBrands.Count > 0)
        {
            y += 10;
            Line("En sık araç markaları", true);
            foreach (var row in report.TopBrands) Line($"{row.Brand}: {row.Vehicles}");
        }
        if (page is not null) document.FinishPage(page);
        await using var stream = File.Create(path);
        document.WriteTo(stream);
        return path;
    }

    public static Task ShareAsync(string path, string title) =>
        Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));

    public static Task<bool> OpenAsync(string path, string title) =>
        Launcher.Default.OpenAsync(new OpenFileRequest(title, new ReadOnlyFile(path)));

    private static string FileName(ReportSummaryDto report, string extension) =>
        $"VALE-Rapor-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.{extension}";

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private static string WorksheetXml(IEnumerable<IReadOnlyList<object?>> rows)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rowNumber = 1;
        var sheetData = new XElement(ns + "sheetData");
        foreach (var row in rows)
        {
            var rowElement = new XElement(ns + "row", new XAttribute("r", rowNumber));
            for (var i = 0; i < row.Count; i++)
            {
                var value = row[i];
                var reference = $"{ColumnName(i + 1)}{rowNumber}";
                if (value is null) continue;
                if (value is int or long or double or float or decimal)
                {
                    rowElement.Add(new XElement(ns + "c", new XAttribute("r", reference),
                        new XElement(ns + "v", Convert.ToString(value, CultureInfo.InvariantCulture))));
                }
                else
                {
                    rowElement.Add(new XElement(ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"),
                        new XElement(ns + "is", new XElement(ns + "t", value.ToString()))));
                }
            }
            sheetData.Add(rowElement); rowNumber++;
        }
        var worksheet = new XElement(ns + "worksheet", sheetData);
        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), worksheet).ToString(SaveOptions.DisableFormatting);
    }

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
        return result;
    }

    private static string PaymentText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Card => "Kart",
        PaymentMethod.Transfer => "Havale/EFT",
        _ => method.ToString()
    };
}
