using Microsoft.Maui.Hosting;

namespace VALE.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        // Android tints the entire MAUI field drawable, including its fill.
        // Clear that drawable so fields inherit their surrounding card surface.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent));
#endif
        return MauiApp.CreateBuilder().UseMauiApp<App>().Build();
    }
}
