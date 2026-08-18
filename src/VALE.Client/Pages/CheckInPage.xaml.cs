using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;
using Windows.Storage.Pickers;

namespace VALE.Client.Pages;

public sealed partial class CheckInPage : Page
{
    private const long MaxPhotoBytes = 4_000_000;

    public CheckInViewModel ViewModel { get; }

    public CheckInPage()
    {
        ViewModel = App.Services.GetRequiredService<CheckInViewModel>();
        InitializeComponent();
    }

    private async void PickPhoto_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            await using var input = await file.OpenStreamForReadAsync();
            if (input.Length > MaxPhotoBytes)
            {
                ViewModel.ClearPhoto();
                throw new InvalidOperationException("Fotoğraf en fazla 4 MB olabilir. Daha küçük bir görsel seçin.");
            }

            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            var bytes = memory.ToArray();
            ViewModel.SetPhoto(Convert.ToBase64String(bytes), $"{file.Name}  •  {bytes.Length / 1024d:N0} KB");
        }
        catch (Exception exception)
        {
            var dialog = new ContentDialog
            {
                Title = "Fotoğraf eklenemedi",
                Content = exception.Message,
                CloseButtonText = "Tamam",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void RemovePhoto_Click(object sender, RoutedEventArgs e) => ViewModel.ClearPhoto();
}
