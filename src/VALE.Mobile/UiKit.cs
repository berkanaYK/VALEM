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
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        };
        label.SetDynamicResource(Label.TextColorProperty, secondary ? "ValeSecondary" : "ValeText");
        return label;
    }

    public static Entry Entry(string placeholder, Keyboard? keyboard = null, bool password = false)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            Keyboard = keyboard ?? Keyboard.Default,
            IsPassword = password,
            HeightRequest = 52,
            Margin = new Thickness(0, 2),
            ClearButtonVisibility = password ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing
        };
        entry.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeSoftCard");
        entry.SetDynamicResource(Entry.TextColorProperty, "ValeText");
        entry.SetDynamicResource(Entry.PlaceholderColorProperty, "ValeSecondary");
        return entry;
    }

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        HeightRequest = 52,
        CornerRadius = 15,
        BackgroundColor = ThemeService.Palette.Accent,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        FontSize = 15
    };

    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            HeightRequest = 48,
            CornerRadius = 14,
            BorderWidth = 1,
            BorderColor = ThemeService.Palette.Border,
            FontAttributes = FontAttributes.Bold
        };
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, "ValeCard");
        button.SetDynamicResource(Button.TextColorProperty, "ValeText");
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
        var valueLabel = Label(value, 25, true);
        var content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Label(title, 11, true, true),
                valueLabel,
                Label(caption, 11, false, true)
            }
        };
        return (Card(content, new Thickness(14), 18), valueLabel);
    }

    public static ActivityIndicator Activity() => new()
    {
        Color = ThemeService.Palette.Accent,
        WidthRequest = 24,
        HeightRequest = 24
    };
}
