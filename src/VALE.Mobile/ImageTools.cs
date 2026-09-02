using Android.Graphics;

namespace VALE.Mobile;

public static class ImageTools
{
    public static async Task<byte[]> NormalizeJpegAsync(Stream input, int maxEdge = 1080, int quality = 82)
    {
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);
        var source = BitmapFactory.DecodeByteArray(memory.ToArray(), 0, (int)memory.Length)
            ?? throw new InvalidOperationException("Görsel okunamadı.");
        using (source)
        {
            var scale = Math.Min(1d, maxEdge / (double)Math.Max(source.Width, source.Height));
            using var bitmap = (scale < 1
                ? Bitmap.CreateScaledBitmap(source, Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), true)
                : source.Copy(Bitmap.Config.Argb8888!, false))
                ?? throw new InvalidOperationException("Görsel işlenemedi.");
            using var output = new MemoryStream();
            var jpegFormat = Bitmap.CompressFormat.Jpeg
                ?? throw new InvalidOperationException("JPEG sıkıştırma biçimi kullanılamıyor.");
            if (!bitmap.Compress(jpegFormat, quality, output))
                throw new InvalidOperationException("Görsel küçültülemedi.");
            return output.ToArray();
        }
    }

    public static (ValeAccent Accent, bool Dark) Analyze(byte[] jpeg)
    {
        using var bitmap = BitmapFactory.DecodeByteArray(jpeg, 0, jpeg.Length)
            ?? throw new InvalidOperationException("Görsel renkleri okunamadı.");
        long red = 0, green = 0, blue = 0, count = 0;
        var stepX = Math.Max(1, bitmap.Width / 32);
        var stepY = Math.Max(1, bitmap.Height / 32);
        for (var y = 0; y < bitmap.Height; y += stepY)
        for (var x = 0; x < bitmap.Width; x += stepX)
        {
            var color = bitmap.GetPixel(x, y);
            red += Android.Graphics.Color.GetRedComponent(color);
            green += Android.Graphics.Color.GetGreenComponent(color);
            blue += Android.Graphics.Color.GetBlueComponent(color);
            count++;
        }
        var r = red / (double)count; var g = green / (double)count; var b = blue / (double)count;
        var luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255d;
        var accent = g > r * 1.12 && g > b ? ValeAccent.Emerald
            : r > g * 1.2 && r > b * 1.15 ? ValeAccent.Orange
            : b > r * 1.12 ? ValeAccent.Indigo : ValeAccent.Blue;
        return (accent, luminance < 0.58);
    }
}
