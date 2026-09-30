using Microsoft.Maui.Controls.Shapes;

namespace VALE.Mobile;

/// <summary>A keyboard-accessible toggle with a 240 ms sliding thumb.</summary>
public sealed class SmoothToggle : ContentView
{
    public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(
        nameof(IsToggled), typeof(bool), typeof(SmoothToggle), false,
        propertyChanged: (view, _, _) => ((SmoothToggle)view).Update());
    private readonly Border _track;
    private readonly Border _thumb;
    private readonly Button _button;

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public SmoothToggle()
    {
        WidthRequest = 56; HeightRequest = 48;
        _thumb = new Border
        {
            WidthRequest = 22, HeightRequest = 22, BackgroundColor = Colors.White,
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 },
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center
        };
        _track = new Border
        {
            WidthRequest = 50, HeightRequest = 28, Padding = 3,
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = _thumb, VerticalOptions = LayoutOptions.Center
        };
        _button = new Button { BackgroundColor = Colors.Transparent, Padding = 0, BorderWidth = 0 };
        _button.Clicked += (_, _) => IsToggled = !IsToggled;
        var grid = new Grid(); grid.Add(_track); grid.Add(_button); Content = grid;
        Loaded += (_, _) => Update(animate: false);
        Update(animate: false);
    }

    private void Update(bool animate = true)
    {
        if (_thumb is null) return;
        _thumb.CancelAnimations();
        _track.BackgroundColor = IsToggled ? ThemeService.Palette.Accent : Color.FromArgb("#64748B");
        SemanticProperties.SetDescription(_button, IsToggled ? "Oturumu açık tut: Açık" : "Oturumu açık tut: Kapalı");
        SemanticProperties.SetDescription(this, IsToggled ? "Oturumu açık tut: Açık" : "Oturumu açık tut: Kapalı");
        var offset = IsToggled ? 22 : 0;
        if (animate && IsLoaded) _ = _thumb.TranslateToAsync(offset, 0, 240, Easing.CubicInOut);
        else _thumb.TranslationX = offset;
    }
}
