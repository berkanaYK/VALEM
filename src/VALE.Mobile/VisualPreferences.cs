using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace VALE.Mobile;

public static class VisualPreferences
{
    private const string BackgroundPathKey = "vale_background_image_v31";
    public static string? BackgroundPath => Preferences.Default.Get(BackgroundPathKey, string.Empty) is { Length: > 0 } path && File.Exists(path) ? path : null;

    public static void ApplyBackground(ContentPage page)
    {
        var path = BackgroundPath;
        page.BackgroundImageSource = path is null ? null : ImageSource.FromFile(path);
    }

    public static async Task<string> SaveBackgroundAsync(FileResult file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
        var path = Path.Combine(FileSystem.AppDataDirectory, "vale-background" + extension.ToLowerInvariant());
        foreach (var old in Directory.GetFiles(FileSystem.AppDataDirectory, "vale-background.*"))
            if (!string.Equals(old, path, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
        await using var input = await file.OpenReadAsync();
        await using var output = File.Create(path);
        await input.CopyToAsync(output);
        Preferences.Default.Set(BackgroundPathKey, path);
        return path;
    }

    public static void ClearBackground()
    {
        var path = BackgroundPath;
        Preferences.Default.Remove(BackgroundPathKey);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }
}

public sealed class AppearancePageV31 : ContentPage
{
    private readonly Label _status = UiKit.Label("Arka plan kullanılmıyor.", 11.5, false, true);

    public AppearancePageV31()
    {
        Title = "Görünüm";
        UiKit.StylePage(this);
        RefreshStatus();

        var choose = UiKit.PrimaryButton("Arka Plan Fotoğrafı Seç");
        choose.Clicked += async (_, _) =>
        {
            try
            {
                var file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "VALE arka planı seçin",
                    FileTypes = FilePickerFileType.Images
                });
                if (file is null) return;
                await VisualPreferences.SaveBackgroundAsync(file);
                VisualPreferences.ApplyBackground(this);
                RefreshStatus();
                await DisplayAlertAsync("Görünüm", "Arka plan kaydedildi. Yeni açılan ekranlarda da kullanılacak.", "Tamam");
            }
            catch (Exception ex) { await DisplayAlertAsync("Görünüm", ex.Message, "Tamam"); }
        };

        var remove = UiKit.SecondaryButton("Arka Planı Kaldır");
        remove.Clicked += async (_, _) =>
        {
            VisualPreferences.ClearBackground();
            VisualPreferences.ApplyBackground(this);
            RefreshStatus();
            await DisplayAlertAsync("Görünüm", "Özel arka plan kaldırıldı.", "Tamam");
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18, Spacing = 13,
                Children =
                {
                    UiKit.Label("Görünüm ayarları", 27, true),
                    UiKit.Label("Tema ve vurgu renginize ek olarak cihazınızdan kişisel bir arka plan görseli seçebilirsiniz. İçerik kartları okunabilirlik için opak kalır.", 12, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { _status, choose, remove } })
                }
            }
        };
    }

    private void RefreshStatus() => _status.Text = VisualPreferences.BackgroundPath is null ? "Arka plan kullanılmıyor." : "Özel arka plan etkin.";
}
