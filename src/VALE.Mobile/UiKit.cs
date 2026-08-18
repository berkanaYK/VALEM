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

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        MinimumHeightRequest = 52,
        Padding = new Thickness(16, 11),
        CornerRadius = 14,
        BackgroundColor = ThemeService.Palette.Accent,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        FontSize = 14.5,
        FontAutoScalingEnabled = true,
        LineBreakMode = LineBreakMode.WordWrap,
        HorizontalOptions = LayoutOptions.Fill
    };

    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            MinimumHeightRequest = 50,
            Padding = new Thickness(14, 10),
            CornerRadius = 14,
            BorderWidth = 1,
            BorderColor = ThemeService.Palette.Border,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            FontAutoScalingEnabled = true,
            LineBreakMode = LineBreakMode.WordWrap,
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

    public static Border Card(View content, Thickness? padding = null, float radius = 18)
    {
        var border = new Border
        {
            Stroke = ThemeService.Palette.Border,
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
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        return border;
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

    public static ActivityIndicator Activity() => new()
    {
        Color = ThemeService.Palette.Accent,
        WidthRequest = 24,
        HeightRequest = 24
    };
}
