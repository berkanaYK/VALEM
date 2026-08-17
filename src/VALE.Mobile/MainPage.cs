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
            await DisplayAlertAsync("Giriş başarısız", ex.Message, "Tamam");
        }
    }

    private void BuildHome()
    {
        _summary = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
        _searchEntry = new Entry { Placeholder = "Plaka, fiş veya telefon ara", HorizontalOptions = LayoutOptions.FillAndExpand };
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
                var state = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.End };
                state.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.Status));
                var amount = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.End };
                amount.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.AmountDue), stringFormat: "{0:N2} TL"));

                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto)
                    }
                };
                row.Add(plate, 0, 0);
                row.Add(state, 1, 0);
                row.Add(amount, 1, 1);

                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = 12,
                    Margin = new Thickness(0, 4),
                    Content = row
                };
            })
        };
        _list.SelectionChanged += TicketSelected;

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        actions.Add(_searchEntry, 0, 0);
        actions.Add(refresh, 1, 0);
        actions.Add(add, 2, 0);

        var root = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 10
        };
        root.Add(_summary, 0, 0);
        root.Add(actions, 0, 1);
        root.Add(new Label { Text = "Aktif araçlar", FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 2);
        root.Add(_list, 0, 3);
        Content = root;
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
            await DisplayAlertAsync("Yenileme başarısız", ex.Message, "Tamam");
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
            await DisplayAlertAsync("Kayıt başarısız", ex.Message, "Tamam");
        }
    }

    private async void TicketSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TicketSummaryDto ticket) return;
        _list.SelectedItem = null;
        var choice = await DisplayActionSheetAsync(ticket.LicensePlate, "Kapat", null, "Park edildi", "Araç isteniyor", "Nakit teslim", "Kart teslim", "Havale/EFT teslim", "İptal");
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
            await DisplayAlertAsync("İşlem başarısız", ex.Message, "Tamam");
        }
    }
}
