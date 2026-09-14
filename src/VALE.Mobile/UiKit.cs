using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace VALE.Mobile;

public static class UiKit
{
    public static void StylePage(ContentPage page)
    {
        page.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValePage");
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName != nameof(ContentPage.Content) || page.Content is null || page.Content is ThemeBackgroundHost) return;
            page.PropertyChanged -= handler;
            page.Content = new ThemeBackgroundHost(page.Content);
        };
        page.PropertyChanged += handler;
    }

    public static Label Label(string text, double size = 14, bool bold = false, bool secondary = false)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            FontAutoScalingEnabled = true,
            LineBreakMode = LineBreakMode.WordWrap
        };
        label.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, secondary ? "ValeSecondary" : "ValeText");
        return label;
    }

    public static Entry Entry(string placeholder, Keyboard? keyboard = null, bool password = false)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            Keyboard = keyboard ?? Keyboard.Default,
            IsPassword = password,
            MinimumHeightRequest = 52,
            FontSize = 15,
            FontAutoScalingEnabled = true,
            Margin = new Thickness(0, 1),
            HorizontalOptions = LayoutOptions.Fill,
            ClearButtonVisibility = password ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing
        };
        entry.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        entry.SetDynamicResource(Microsoft.Maui.Controls.Entry.TextColorProperty, "ValeText");
        entry.SetDynamicResource(Microsoft.Maui.Controls.Entry.PlaceholderColorProperty, "ValeSecondary");
        return entry;
    }

    public static Editor Editor(string placeholder)
    {
        var editor = new Editor
        {
            Placeholder = placeholder,
            MinimumHeightRequest = 92,
            AutoSize = EditorAutoSizeOption.TextChanges,
            FontSize = 15,
            FontAutoScalingEnabled = true,
            HorizontalOptions = LayoutOptions.Fill
        };
        editor.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        editor.SetDynamicResource(Microsoft.Maui.Controls.Editor.TextColorProperty, "ValeText");
        editor.SetDynamicResource(Microsoft.Maui.Controls.Editor.PlaceholderColorProperty, "ValeSecondary");
        return editor;
    }

    public static Picker Picker(string title)
    {
        var picker = new Microsoft.Maui.Controls.Picker
        {
            Title = title,
            MinimumHeightRequest = 52,
            FontSize = 15,
            FontAutoScalingEnabled = true,
            HorizontalOptions = LayoutOptions.Fill
        };
        picker.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        picker.SetDynamicResource(Microsoft.Maui.Controls.Picker.TextColorProperty, "ValeText");
        picker.SetDynamicResource(Microsoft.Maui.Controls.Picker.TitleColorProperty, "ValeSecondary");
        return picker;
    }

    public static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            MinimumHeightRequest = 52,
            Padding = new Thickness(16, 11),
            CornerRadius = 14,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14.5,
            FontAutoScalingEnabled = true,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalOptions = LayoutOptions.Fill
        };
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeAccent");
        return button;
    }

    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            MinimumHeightRequest = 50,
            Padding = new Thickness(14, 10),
            CornerRadius = 14,
            BorderWidth = 1,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            FontAutoScalingEnabled = true,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalOptions = LayoutOptions.Fill
        };
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        button.SetDynamicResource(Button.TextColorProperty, "ValeText");
        button.SetDynamicResource(Button.BorderColorProperty, "ValeBorder");
        return button;
    }

    public static Button TextButton(string text)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            MinimumHeightRequest = 46,
            Padding = new Thickness(8, 7),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            FontAutoScalingEnabled = true,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalOptions = LayoutOptions.Fill
        };
        button.SetDynamicResource(Button.TextColorProperty, "ValeAccent");
        return button;
    }

    public static Button DangerTextButton(string text)
    {
        var button = TextButton(text);
        button.SetDynamicResource(Button.TextColorProperty, "ValeDanger");
        return button;
    }

    public static Switch Switch(bool isToggled = false)
    {
        var control = new Microsoft.Maui.Controls.Switch { IsToggled = isToggled };
        control.SetDynamicResource(Microsoft.Maui.Controls.Switch.OnColorProperty, "ValeAccent");
        return control;
    }

    public static Border Card(View content, Thickness? padding = null, float radius = 18)
    {
        var border = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = radius },
            Padding = padding ?? new Thickness(16),
            HorizontalOptions = LayoutOptions.Fill,
            Content = content,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#120F172A")),
                Offset = new Point(0, 2),
                Radius = 8,
                Opacity = 0.18f
            }
        };
        border.SetDynamicResource(Border.StrokeProperty, "ValeBorderBrush");
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        return border;
    }

    public static BoxView Divider()
    {
        var divider = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 4) };
        divider.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeBorder");
        return divider;
    }

    public static (Border Card, Label Value) Metric(string title, string value, string caption)
    {
        var valueLabel = Label(value, 21, true);
        valueLabel.LineBreakMode = LineBreakMode.WordWrap;
        valueLabel.MaxLines = 2;
        valueLabel.MinimumHeightRequest = 30;
        valueLabel.VerticalTextAlignment = TextAlignment.Center;

        var titleLabel = Label(title, 10.5, true, true);
        titleLabel.MaxLines = 2;
        var captionLabel = Label(caption, 10.5, false, true);
        captionLabel.MaxLines = 2;

        var content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                titleLabel,
                valueLabel,
                captionLabel
            }
        };
        return (Card(content, new Thickness(13), 16), valueLabel);
    }

    public static ActivityIndicator Activity()
    {
        var activity = new ActivityIndicator
        {
            WidthRequest = 24,
            HeightRequest = 24
        };
        activity.SetDynamicResource(ActivityIndicator.ColorProperty, "ValeAccent");
        return activity;
    }
}

public sealed class ThemeBackgroundHost : Grid
{
    private readonly Image _background = new() { Aspect = Aspect.AspectFill, Opacity = 0.92 };
    private readonly BoxView _overlay = new() { Color = Color.FromRgba(4, 10, 20, 118) };

    public ThemeBackgroundHost(View content)
    {
        Add(_background);
        Add(_overlay);
        Add(content);
        Loaded += (_, _) => { ThemeService.Changed -= OnThemeChanged; ThemeService.Changed += OnThemeChanged; Refresh(); };
        Unloaded += (_, _) => ThemeService.Changed -= OnThemeChanged;
        _background.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(31) && _background.Handler?.PlatformView is Android.Views.View native)
                native.SetRenderEffect(Android.Graphics.RenderEffect.CreateBlurEffect(12f, 12f, Android.Graphics.Shader.TileMode.Clamp!));
#endif
        };
        Refresh();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(Refresh);

    private void Refresh()
    {
        var source = ThemeService.CurrentBackground switch
        {
            ValeBackgroundTheme.AnimeNeon => ImageSource.FromFile("theme_anime_neon.jpg"),
            ValeBackgroundTheme.AnimeSunset => ImageSource.FromFile("theme_anime_sunset.jpg"),
            ValeBackgroundTheme.CarNeon => ImageSource.FromFile("theme_car_neon.jpg"),
            ValeBackgroundTheme.CarTrack => ImageSource.FromFile("theme_car_track.jpg"),
            ValeBackgroundTheme.Custom when File.Exists(ThemeService.CustomBackgroundPath) =>
                ImageSource.FromStream(() => File.OpenRead(ThemeService.CustomBackgroundPath!)),
            _ => null
        };
        _background.Source = source;
        _background.IsVisible = source is not null;
        _overlay.IsVisible = source is not null;
    }
}
