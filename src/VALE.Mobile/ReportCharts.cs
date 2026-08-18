using Microsoft.Maui.Graphics;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class DailyRevenueDrawable : IDrawable
{
    public IReadOnlyList<DailyReportPointDto> Points { get; set; } = Array.Empty<DailyReportPointDto>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        var palette = ThemeService.Palette;
        var left = 34f;
        var right = 10f;
        var top = 14f;
        var bottom = 34f;
        var width = Math.Max(1, dirtyRect.Width - left - right);
        var height = Math.Max(1, dirtyRect.Height - top - bottom);
        canvas.StrokeColor = palette.Border;
        canvas.StrokeSize = 1;
        canvas.DrawLine(left, top + height, left + width, top + height);

        if (Points.Count == 0)
        {
            canvas.FontColor = palette.Secondary;
            canvas.FontSize = 12;
            canvas.DrawString("Bu tarih aralığında veri yok", left, top, width, height, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.RestoreState();
            return;
        }

        var max = Math.Max(1m, Points.Max(x => x.Revenue));
        var gap = Points.Count <= 14 ? 5f : 2f;
        var barWidth = Math.Max(1.5f, (width - gap * Math.Max(0, Points.Count - 1)) / Points.Count);
        canvas.FillColor = palette.Accent;
        for (var i = 0; i < Points.Count; i++)
        {
            var value = (float)(Points[i].Revenue / max);
            var barHeight = Math.Max(2f, height * value);
            var x = left + i * (barWidth + gap);
            canvas.FillRoundedRectangle(x, top + height - barHeight, barWidth, barHeight, Math.Min(4, barWidth / 2));
        }

        canvas.FontColor = palette.Secondary;
        canvas.FontSize = 9;
        var labelEvery = Math.Max(1, (int)Math.Ceiling(Points.Count / 6d));
        for (var i = 0; i < Points.Count; i += labelEvery)
        {
            var x = left + i * (barWidth + gap) - 14;
            canvas.DrawString(Points[i].Day.ToString("dd.MM"), x, top + height + 7, 38, 18, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
        canvas.RestoreState();
    }
}

public sealed class HourlyDeliveryDrawable : IDrawable
{
    public IReadOnlyList<HourlyReportPointDto> Points { get; set; } = Array.Empty<HourlyReportPointDto>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        var palette = ThemeService.Palette;
        var left = 28f;
        var top = 14f;
        var bottom = 30f;
        var width = Math.Max(1, dirtyRect.Width - left - 8);
        var height = Math.Max(1, dirtyRect.Height - top - bottom);
        var points = Points.Count == 24 ? Points : Enumerable.Range(0, 24).Select(x => new HourlyReportPointDto(x, 0, 0)).ToList();
        var max = Math.Max(1, points.Max(x => x.Delivered));
        var gap = 3f;
        var barWidth = Math.Max(2f, (width - gap * 23) / 24f);
        canvas.FillColor = palette.Success;
        for (var i = 0; i < 24; i++)
        {
            var h = Math.Max(2f, height * points[i].Delivered / max);
            var x = left + i * (barWidth + gap);
            canvas.FillRoundedRectangle(x, top + height - h, barWidth, h, Math.Min(3, barWidth / 2));
        }
        canvas.StrokeColor = palette.Border;
        canvas.DrawLine(left, top + height, left + width, top + height);
        canvas.FontColor = palette.Secondary;
        canvas.FontSize = 9;
        foreach (var hour in new[] { 0, 6, 12, 18, 23 })
        {
            var x = left + hour * (barWidth + gap) - 10;
            canvas.DrawString($"{hour:00}", x, top + height + 6, 28, 18, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
        canvas.RestoreState();
    }
}
