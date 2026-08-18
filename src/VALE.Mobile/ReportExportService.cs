using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using VALE.Contracts;

namespace VALE.Mobile;

public static class ReportExportService
{
    public static async Task<string> WriteCsvAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"VALE_Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Tarih;Araç;Teslim;Ciro");
        foreach (var row in report.Daily)
            sb.AppendLine($"{row.Day:dd.MM.yyyy};{row.Vehicles};{row.Delivered};{row.Revenue:0.00}");
        sb.AppendLine();
        sb.AppendLine($"Toplam Araç;{report.Vehicles}");
        sb.AppendLine($"Toplam Teslim;{report.Delivered}");
        sb.AppendLine($"İptal;{report.Cancelled}");
        sb.AppendLine($"Ciro;{report.Revenue:0.00}");
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public static async Task<string> WriteXlsxAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"VALE_Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        if (File.Exists(path)) File.Delete(path);
        await using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);

        await WriteEntryAsync(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        await WriteEntryAsync(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        await WriteEntryAsync(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="VALE Rapor" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        await WriteEntryAsync(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetData = new XElement(ns + "sheetData");
        var rowNumber = 1;
        AddRow(sheetData, rowNumber++, "VALE Rapor", $"{report.From:dd.MM.yyyy} - {report.To:dd.MM.yyyy}");
        AddRow(sheetData, rowNumber++, "Toplam Araç", report.Vehicles.ToString(), "Toplam Teslim", report.Delivered.ToString());
        AddRow(sheetData, rowNumber++, "İptal", report.Cancelled.ToString(), "Ciro", report.Revenue.ToString("0.00") + " TL");
        AddRow(sheetData, rowNumber++, "Ortalama Tutar", report.AverageAmount.ToString("0.00") + " TL", "Ortalama Süre", report.AverageParkingMinutes.ToString("0") + " dk");
        rowNumber++;
        AddRow(sheetData, rowNumber++, "Tarih", "Araç", "Teslim", "Ciro (TL)");
        foreach (var point in report.Daily)
            AddRow(sheetData, rowNumber++, point.Day.ToString("dd.MM.yyyy"), point.Vehicles.ToString(), point.Delivered.ToString(), point.Revenue.ToString("0.00"));

        var worksheet = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ns + "worksheet", sheetData));
        await WriteEntryAsync(zip, "xl/worksheets/sheet1.xml", worksheet.ToString(SaveOptions.DisableFormatting));
        return path;
    }

    public static async Task<string> WritePdfAsync(ReportSummaryDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"VALE_Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        var lines = new List<string>
        {
            "VALE RAPORU",
            $"Donem: {Ascii($"{report.From:dd.MM.yyyy} - {report.To:dd.MM.yyyy}")}",
            $"Toplam Arac: {report.Vehicles}",
            $"Teslim: {report.Delivered}",
            $"Iptal: {report.Cancelled}",
            $"Ciro: {report.Revenue:0.00} TL",
            $"Ortalama Tutar: {report.AverageAmount:0.00} TL",
            $"Ortalama Park Suresi: {report.AverageParkingMinutes:0} dk",
            "",
            "TARIH       ARAC   TESLIM   CIRO"
        };
        foreach (var point in report.Daily.Take(28))
            lines.Add($"{point.Day:dd.MM.yyyy}   {point.Vehicles,4}   {point.Delivered,6}   {point.Revenue,8:0.00}");
        lines.Add("");
        lines.Add("Not: PDF onizleme uyumlulugu icin metin ASCII olarak uretilmistir. Excel/CSV Turkce karakterleri korur.");

        var content = new StringBuilder("BT\n/F1 11 Tf\n50 790 Td\n");
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) content.Append("0 -18 Td\n");
            content.Append('(').Append(EscapePdf(lines[i])).Append(") Tj\n");
        }
        content.Append("ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(Concat(Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n"), stream, Encoding.ASCII.GetBytes("endstream")));

        await using var output = File.Create(path);
        await output.WriteAsync(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            await output.WriteAsync(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            await output.WriteAsync(objects[i]);
            await output.WriteAsync(Encoding.ASCII.GetBytes("\nendobj\n"));
        }
        var xref = output.Position;
        await output.WriteAsync(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1))
            await output.WriteAsync(Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n"));
        await output.WriteAsync(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return path;
    }

    private static void AddRow(XElement sheetData, int rowNumber, params string[] values)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var row = new XElement(ns + "row", new XAttribute("r", rowNumber));
        for (var i = 0; i < values.Length; i++)
        {
            var reference = $"{ColumnName(i + 1)}{rowNumber}";
            row.Add(new XElement(ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"),
                new XElement(ns + "is", new XElement(ns + "t", values[i] ?? string.Empty))));
        }
        sheetData.Add(row);
    }

    private static string ColumnName(int index)
    {
        var result = string.Empty;
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    private static async Task WriteEntryAsync(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content);
    }

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static byte[] Concat(params byte[][] values)
    {
        var result = new byte[values.Sum(x => x.Length)];
        var offset = 0;
        foreach (var value in values) { Buffer.BlockCopy(value, 0, result, offset, value.Length); offset += value.Length; }
        return result;
    }

    private static string Ascii(string value) => value
        .Replace('ç', 'c').Replace('Ç', 'C').Replace('ğ', 'g').Replace('Ğ', 'G')
        .Replace('ı', 'i').Replace('İ', 'I').Replace('ö', 'o').Replace('Ö', 'O')
        .Replace('ş', 's').Replace('Ş', 'S').Replace('ü', 'u').Replace('Ü', 'U');
}
