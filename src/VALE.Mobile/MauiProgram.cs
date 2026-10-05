using Microsoft.Maui.Hosting;

namespace VALE.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        // Remove Android's native underline while preserving MAUI's theme-aware fill.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("ValeField", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
        return MauiApp.CreateBuilder().UseMauiApp<App>()
#if ANDROID
            .ConfigureMauiHandlers(handlers => handlers.AddHandler(typeof(Shell), typeof(ValeShellRenderer)))
#endif
            .Build();
    }
}
