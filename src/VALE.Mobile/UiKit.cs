using Microsoft.Maui.Controls;

namespace VALE.Mobile;

public static class UiKit
{
    public static void StylePage(ContentPage page)
    {
        page.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValePage");
    }

    public static Label Label(string text, double size = 14, bool bold = false, bool secondary = false)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
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
            MinimumHeightRequest = 54,
            FontSize = 15,
            Margin = new Thickness(0, 2),
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
            FontSize = 15
        };
        editor.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        editor.SetDynamicResource(Microsoft.Maui.Controls.Editor.TextColorProperty, "ValeText");
        editor.SetDynamicResource(Microsoft.Maui.Controls.Editor.PlaceholderColorProperty, "ValeSecondary");
        return editor;
    }

    public static Picker Picker(string title)
    {
        var picker = new Picker
        {
            Title = title,
            MinimumHeightRequest = 54,
            FontSize = 15
        };
        picker.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        picker.SetDynamicResource(Picker.TextColorProperty, "ValeText");
        picker.SetDynamicResource(Picker.TitleColorProperty, "ValeSecondary");
        return picker;
    }

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        MinimumHeightRequest = 54,
        Padding = new Thickness(18, 12),
        CornerRadius = 16,
        BackgroundColor = ThemeService.Palette.Accent,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        FontSize = 15,
        HorizontalOptions = LayoutOptions.Fill
    };

    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            MinimumHeightRequest = 52,
            Padding = new Thickness(16, 11),
            CornerRadius = 15,
            BorderWidth = 1,
            BorderColor = ThemeService.Palette.Border,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Fill
        };
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        button.SetDynamicResource(Button.TextColorProperty, "ValeText");
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
            Padding = new Thickness(10, 8),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill
        };
        button.SetDynamicResource(Button.TextColorProperty, "ValeAccent");
        return button;
    }

    public static Border Card(View content, Thickness? padding = null, float radius = 20)
    {
        var border = new Border
        {
            Stroke = ThemeService.Palette.Border,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = radius },
            Padding = padding ?? new Thickness(16),
            Content = content
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        return border;
    }

    public static (Border Card, Label Value) Metric(string title, string value, string caption)
    {
        var valueLabel = Label(value, 23, true);
        valueLabel.LineBreakMode = LineBreakMode.TailTruncation;
        var content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Label(title, 10.5, true, true),
                valueLabel,
                Label(caption, 10.5, false, true)
            }
        };
        return (Card(content, new Thickness(13), 18), valueLabel);
    }

    public static ActivityIndicator Activity() => new()
    {
        Color = ThemeService.Palette.Accent,
        WidthRequest = 24,
        HeightRequest = 24
    };
}
