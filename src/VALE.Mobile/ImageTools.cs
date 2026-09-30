using Android.Graphics;

namespace VALE.Mobile;

public static class ImageTools
{
    public static async Task<byte[]> NormalizeJpegAsync(Stream input, int maxEdge = 1080, int quality = 82)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            if (memory.Length + read > 25_000_000) throw new UserFacingException("Görsel en fazla 25 MB olabilir. Daha küçük bir görsel seçin.");
            await memory.WriteAsync(buffer.AsMemory(0, read));
        }
        var bytes = memory.ToArray();
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds)?.Dispose();
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) throw new UserFacingException("Görsel okunamadı. JPEG veya PNG fotoğraf seçin.");
        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > maxEdge * 2) sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample };
        var source = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options)
            ?? throw new InvalidOperationException("Görsel okunamadı.");
        using (source)
        {
            var scale = Math.Min(1d, maxEdge / (double)Math.Max(source.Width, source.Height));
            using var bitmap = (scale < 1
                ? Bitmap.CreateScaledBitmap(source, Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), true)
                : source.Copy(Bitmap.Config.Argb8888!, false))
                ?? throw new InvalidOperationException("Görsel işlenemedi.");
            using var exifInput = new MemoryStream(bytes, writable: false);
            using var exif = new Android.Media.ExifInterface(exifInput);
            var orientation = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1);
            using var matrix = new Matrix();
            switch (orientation)
            {
                case 2: matrix.SetScale(-1, 1); break;
                case 3: matrix.SetRotate(180); break;
                case 4: matrix.SetScale(1, -1); break;
                case 5: matrix.SetRotate(90); matrix.PostScale(-1, 1); break;
                case 6: matrix.SetRotate(90); break;
                case 7: matrix.SetRotate(270); matrix.PostScale(-1, 1); break;
                case 8: matrix.SetRotate(270); break;
            }
            using var oriented = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true)
                ?? throw new UserFacingException("Fotoğrafın yönü düzenlenemedi.");
            using var output = new MemoryStream();
            var jpegFormat = Bitmap.CompressFormat.Jpeg
                ?? throw new InvalidOperationException("JPEG sıkıştırma biçimi kullanılamıyor.");
            if (!oriented.Compress(jpegFormat, quality, output))
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
            : b > r * 1.12 ? ValeAccent.Indigo : ValeAccent.Copper;
        return (accent, luminance < 0.58);
    }
}
