using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class MainPage : ContentPage
{
    private readonly ApiClient _api = new();
    private readonly ObservableCollection<TicketSummaryDto> _tickets = new();
    private Entry _apiEntry = null!;
    private Entry _emailEntry = null!;
    private Entry _passwordEntry = null!;
    private Entry _searchEntry = null!;
    private Label _summary = null!;
    private CollectionView _list = null!;

    public MainPage()
    {
        Title = "VALE";
        BuildLogin();
    }

    private void BuildLogin()
    {
        _apiEntry = new Entry { Placeholder = "https://api-adresi/", Text = _api.SavedBaseUrl, Keyboard = Keyboard.Url };
        _emailEntry = new Entry { Placeholder = "E-posta", Keyboard = Keyboard.Email };
        _passwordEntry = new Entry { Placeholder = "Parola", IsPassword = true };
        var login = new Button { Text = "Giriş Yap" };
        login.Clicked += async (_, _) => await LoginAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 14,
                Children =
                {
                    new Label { Text = "VALE", FontSize = 42, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "Ortak vale yönetim sistemi", HorizontalTextAlignment = TextAlignment.Center },
                    _apiEntry, _emailEntry, _passwordEntry, login
                }
            }
        };
    }

    private async Task LoginAsync()
    {
        try
        {
            _api.Configure(_apiEntry.Text ?? string.Empty);
            await _api.LoginAsync(_emailEntry.Text ?? string.Empty, _passwordEntry.Text ?? string.Empty);
            BuildHome();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Giriş başarısız", ex.Message, "Tamam");
        }
    }

    private void BuildHome()
    {
        _summary = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
        _searchEntry = new Entry { Placeholder = "Plaka, fiş veya telefon ara" };
        var refresh = new Button { Text = "Yenile" };
        refresh.Clicked += async (_, _) => await RefreshAsync();
        var add = new Button { Text = "+ Yeni Araç" };
        add.Clicked += async (_, _) => await AddTicketAsync();

        _list = new CollectionView
        {
            ItemsSource = _tickets,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var plate = new Label { FontAttributes = FontAttributes.Bold, FontSize = 18 };
                plate.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.LicensePlate));
                var state = new Label { FontSize = 12 };
                state.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.Status));
                var amount = new Label { FontSize = 12 };
                amount.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.AmountDue), stringFormat: "{0:N2} TL"));
                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = 12,
                    Margin = new Thickness(0, 4),
                    Content = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        Children = { plate, state, amount }
                    }
                };
            })
        };
        _list.SelectionChanged += TicketSelected;

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)
            },
            Children =
            {
                _summary,
                new HorizontalStackLayout { Spacing = 8, Children = { _searchEntry, refresh, add } },
                new Label { Text = "Aktif araçlar", FontSize = 22, FontAttributes = FontAttributes.Bold },
                _list
            }
        };
        Grid.SetRow(((Grid)Content).Children[1], 1);
        Grid.SetRow(((Grid)Content).Children[2], 2);
        Grid.SetRow(((Grid)Content).Children[3], 3);
    }

    private async Task RefreshAsync()
    {
        try
        {
            var dashboard = await _api.GetDashboardAsync();
            var tickets = await _api.GetTicketsAsync(_searchEntry?.Text);
            _summary.Text = $"{dashboard.BranchName} • Aktif {dashboard.ActiveVehicles} • İstenen {dashboard.WaitingForDelivery} • Bugün {dashboard.DeliveredToday} • {dashboard.RevenueToday:N2} TL";
            _tickets.Clear();
            foreach (var ticket in tickets.Items) _tickets.Add(ticket);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Yenileme başarısız", ex.Message, "Tamam");
        }
    }

    private async Task AddTicketAsync()
    {
        var plate = await DisplayPromptAsync("Yeni araç", "Plaka", "Kaydet", "Vazgeç");
        if (string.IsNullOrWhiteSpace(plate)) return;
        try
        {
            await _api.CreateTicketAsync(new CreateTicketRequest(null, plate.Trim().ToUpperInvariant(), null, null, null, null, null, null, null, null, null));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Kayıt başarısız", ex.Message, "Tamam");
        }
    }

    private async void TicketSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TicketSummaryDto ticket) return;
        _list.SelectedItem = null;
        var choice = await DisplayActionSheet(ticket.LicensePlate, "Kapat", null, "Park edildi", "Araç isteniyor", "Nakit teslim", "Kart teslim", "Havale/EFT teslim", "İptal");
        try
        {
            switch (choice)
            {
                case "Park edildi": await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Parked); break;
                case "Araç isteniyor": await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Requested); break;
                case "Nakit teslim": await _api.CheckoutAsync(ticket.Id, PaymentMethod.Cash); break;
                case "Kart teslim": await _api.CheckoutAsync(ticket.Id, PaymentMethod.Card); break;
                case "Havale/EFT teslim": await _api.CheckoutAsync(ticket.Id, PaymentMethod.Transfer); break;
                case "İptal": await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Cancelled); break;
                default: return;
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("İşlem başarısız", ex.Message, "Tamam");
        }
    }
}
