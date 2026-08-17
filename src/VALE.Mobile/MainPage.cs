using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class MainPage : ContentPage
{
    private static readonly Color PageColor = Color.FromArgb("#070B12");
    private static readonly Color CardColor = Color.FromArgb("#101827");
    private static readonly Color SoftCardColor = Color.FromArgb("#151F31");
    private static readonly Color AccentColor = Color.FromArgb("#4F8CFF");
    private static readonly Color SecondaryTextColor = Color.FromArgb("#94A3B8");
    private static readonly Color BorderColor = Color.FromArgb("#263449");

    private readonly ApiClient _api = new();
    private readonly ObservableCollection<TicketSummaryDto> _tickets = new();

    private Entry _emailEntry = null!;
    private Entry _passwordEntry = null!;
    private Entry _searchEntry = null!;
    private Button _loginButton = null!;
    private ActivityIndicator _loginActivity = null!;
    private Label _loginStatus = null!;
    private Label _userLabel = null!;
    private Label _branchLabel = null!;
    private Label _activeValue = null!;
    private Label _waitingValue = null!;
    private Label _deliveredValue = null!;
    private Label _revenueValue = null!;
    private CollectionView _list = null!;

    private UserDto? _currentUser;
    private bool _busy;

    public MainPage()
    {
        Title = "VALE";
        BackgroundColor = PageColor;
        BuildLogin();
    }

    private void BuildLogin()
    {
        _emailEntry = CreateEntry("E-posta adresi", Keyboard.Email);
        _passwordEntry = CreateEntry("Parola", Keyboard.Default, true);
        _passwordEntry.Completed += async (_, _) => await LoginAsync();

        _loginActivity = new ActivityIndicator
        {
            Color = AccentColor,
            IsVisible = false,
            IsRunning = false,
            WidthRequest = 24,
            HeightRequest = 24
        };

        _loginButton = new Button
        {
            Text = "Giriş Yap",
            HeightRequest = 54,
            CornerRadius = 14,
            BackgroundColor = AccentColor,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        };
        _loginButton.Clicked += async (_, _) => await LoginAsync();

        _loginStatus = new Label
        {
            Text = "Güvenli bağlantı hazır",
            TextColor = SecondaryTextColor,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var advancedButton = new Button
        {
            Text = "⚙ Bağlantı ayarları",
            BackgroundColor = Colors.Transparent,
            TextColor = SecondaryTextColor,
            FontSize = 12,
            Padding = new Thickness(4)
        };
        advancedButton.Clicked += async (_, _) => await ShowAdvancedServerSettingsAsync();

        var loginCard = new Border
        {
            BackgroundColor = CardColor,
            Stroke = BorderColor,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Hesabınıza giriş yapın",
                        TextColor = Colors.White,
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Şubenizdeki araçları tek ekrandan yönetin.",
                        TextColor = SecondaryTextColor,
                        FontSize = 13
                    },
                    _emailEntry,
                    _passwordEntry,
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 10,
                        Children =
                        {
                            { _loginButton, 0, 0 },
                            { _loginActivity, 1, 0 }
                        }
                    },
                    _loginStatus,
                    advancedButton
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 70, 24, 32),
                Spacing = 26,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = "VALE",
                                FontSize = 50,
                                TextColor = Colors.White,
                                FontAttributes = FontAttributes.Bold,
                                CharacterSpacing = 4,
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = "Akıllı vale yönetim sistemi",
                                FontSize = 15,
                                TextColor = SecondaryTextColor,
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        }
                    },
                    loginCard,
                    new Label
                    {
                        Text = "Araç kabul • Park yönetimi • Teslim • Tahsilat",
                        FontSize = 12,
                        TextColor = SecondaryTextColor,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private async Task LoginAsync()
    {
        if (_busy)
        {
            return;
        }

        try
        {
            SetBusy(true, "Sunucuya bağlanılıyor…");
            var login = await _api.LoginAsync(_emailEntry.Text ?? string.Empty, _passwordEntry.Text ?? string.Empty);
            _currentUser = login.User;
            BuildHome();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _loginStatus.Text = ex.Message;
            _loginStatus.TextColor = Color.FromArgb("#FCA5A5");
            await DisplayAlertAsync("Giriş başarısız", ex.Message, "Tamam");
        }
        finally
        {
            SetBusy(false, "Güvenli bağlantı hazır");
        }
    }

    private async Task ShowAdvancedServerSettingsAsync()
    {
        var value = await DisplayPromptAsync(
            "Bağlantı ayarları",
            "Yalnızca özel bir API sunucusu kullanıyorsanız adresi değiştirin.",
            "Kaydet",
            "Vazgeç",
            initialValue: _api.SavedBaseUrl,
            keyboard: Keyboard.Url);

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _api.Configure(value);
            _loginStatus.Text = "Sunucu ayarı kaydedildi";
            _loginStatus.TextColor = SecondaryTextColor;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Geçersiz adres", ex.Message, "Tamam");
        }
    }

    private void BuildHome()
    {
        _userLabel = new Label
        {
            Text = _currentUser?.FullName ?? "VALE Kullanıcısı",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        _branchLabel = new Label
        {
            Text = _currentUser?.BranchName ?? "Tüm şubeler",
            FontSize = 13,
            TextColor = SecondaryTextColor
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        header.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "Hoş geldin", FontSize = 12, TextColor = SecondaryTextColor },
                _userLabel,
                _branchLabel
            }
        }, 0, 0);

        var addButton = new Button
        {
            Text = "+ Araç",
            BackgroundColor = AccentColor,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            HeightRequest = 46,
            Padding = new Thickness(18, 8)
        };
        addButton.Clicked += async (_, _) => await AddTicketAsync();
        header.Add(addButton, 1, 0);

        var metricGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        var active = CreateMetricCard("AKTİF", "0", "Şu an parkta", "#60A5FA");
        _activeValue = active.Value;
        metricGrid.Add(active.Card, 0, 0);

        var waiting = CreateMetricCard("İSTENEN", "0", "Teslime hazırlanıyor", "#FBBF24");
        _waitingValue = waiting.Value;
        metricGrid.Add(waiting.Card, 1, 0);

        var delivered = CreateMetricCard("BUGÜN TESLİM", "0", "Tamamlanan araç", "#34D399");
        _deliveredValue = delivered.Value;
        metricGrid.Add(delivered.Card, 0, 1);

        var revenue = CreateMetricCard("CİRO", "0 TL", "Bugünkü tahsilat", "#A78BFA");
        _revenueValue = revenue.Value;
        metricGrid.Add(revenue.Card, 1, 1);

        _searchEntry = CreateEntry("Plaka, fiş veya telefon ara", Keyboard.Text);
        _searchEntry.Completed += async (_, _) => await RefreshAsync();

        var searchButton = new Button
        {
            Text = "Ara",
            BackgroundColor = SoftCardColor,
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 50
        };
        searchButton.Clicked += async (_, _) => await RefreshAsync();

        var refreshButton = new Button
        {
            Text = "↻",
            BackgroundColor = SoftCardColor,
            TextColor = Colors.White,
            CornerRadius = 12,
            FontSize = 20,
            WidthRequest = 50,
            HeightRequest = 50
        };
        refreshButton.Clicked += async (_, _) => await RefreshAsync();

        var searchGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        searchGrid.Add(_searchEntry, 0, 0);
        searchGrid.Add(searchButton, 1, 0);
        searchGrid.Add(refreshButton, 2, 0);

        _list = new CollectionView
        {
            ItemsSource = _tickets,
            SelectionMode = SelectionMode.Single,
            EmptyView = new Label
            {
                Text = "Aktif araç bulunamadı.",
                TextColor = SecondaryTextColor,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 28)
            },
            ItemTemplate = new DataTemplate(CreateTicketCard)
        };
        _list.SelectionChanged += TicketSelected;

        var root = new Grid
        {
            Padding = new Thickness(16, 16, 16, 10),
            BackgroundColor = PageColor,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 16
        };
        root.Add(header, 0, 0);
        root.Add(metricGrid, 0, 1);
        root.Add(searchGrid, 0, 2);
        root.Add(new Label
        {
            Text = "Aktif araçlar",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        }, 0, 3);
        root.Add(_list, 0, 4);
        Content = root;
    }

    private View CreateTicketCard()
    {
        var plate = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 19,
            TextColor = Colors.White
        };
        plate.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.LicensePlate));

        var vehicle = new Label
        {
            FontSize = 12,
            TextColor = SecondaryTextColor
        };
        vehicle.SetBinding(Label.TextProperty, nameof(TicketSummaryDto.VehicleDescription));

        var ticketNumber = new Label
        {
            FontSize = 11,
            TextColor = SecondaryTextColor
        };
        ticketNumber.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.TicketNumber), stringFormat: "Fiş #{0}"));

        var statusText = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(9, 4)
        };
        statusText.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.Status), converter: new TicketStatusTextConverter()));

        var statusBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            Content = statusText
        };
        statusBorder.SetBinding(BackgroundColorProperty, new Binding(nameof(TicketSummaryDto.Status), converter: new TicketStatusColorConverter()));

        var amount = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.End
        };
        amount.SetBinding(Label.TextProperty, new Binding(nameof(TicketSummaryDto.AmountDue), stringFormat: "{0:N2} TL"));

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            RowSpacing = 3
        };
        content.Add(plate, 0, 0);
        content.Add(vehicle, 0, 1);
        content.Add(ticketNumber, 0, 2);
        content.Add(statusBorder, 1, 0);
        content.Add(amount, 1, 2);

        return new Border
        {
            BackgroundColor = CardColor,
            Stroke = BorderColor,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 9),
            Content = content
        };
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        try
        {
            _busy = true;
            var dashboardTask = _api.GetDashboardAsync();
            var ticketsTask = _api.GetTicketsAsync(_searchEntry?.Text);
            await Task.WhenAll(dashboardTask, ticketsTask);

            var dashboard = await dashboardTask;
            var tickets = await ticketsTask;

            _branchLabel.Text = dashboard.BranchName;
            _activeValue.Text = dashboard.ActiveVehicles.ToString(CultureInfo.CurrentCulture);
            _waitingValue.Text = dashboard.WaitingForDelivery.ToString(CultureInfo.CurrentCulture);
            _deliveredValue.Text = dashboard.DeliveredToday.ToString(CultureInfo.CurrentCulture);
            _revenueValue.Text = $"{dashboard.RevenueToday:N2} TL";

            _tickets.Clear();
            foreach (var ticket in tickets.Items)
            {
                _tickets.Add(ticket);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Yenileme başarısız", ex.Message, "Tamam");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task AddTicketAsync()
    {
        if (_busy)
        {
            return;
        }

        var plate = await DisplayPromptAsync("Yeni araç", "Plaka", "Devam", "Vazgeç", placeholder: "07 ABC 123");
        if (string.IsNullOrWhiteSpace(plate))
        {
            return;
        }

        var customer = await DisplayPromptAsync("Müşteri", "Ad soyad (isteğe bağlı)", "Devam", "Atla");
        var phone = await DisplayPromptAsync("Müşteri", "Telefon (isteğe bağlı)", "Devam", "Atla", keyboard: Keyboard.Telephone);
        var parking = await DisplayPromptAsync("Park", "Park yeri (isteğe bağlı)", "Kaydet", "Atla");

        try
        {
            _busy = true;
            await _api.CreateTicketAsync(new CreateTicketRequest(
                null,
                plate.Trim().ToUpperInvariant(),
                null,
                null,
                null,
                NullIfEmpty(customer),
                NullIfEmpty(phone),
                null,
                NullIfEmpty(parking),
                null,
                null));
            await RefreshAfterMutationAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Kayıt başarısız", ex.Message, "Tamam");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void TicketSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TicketSummaryDto ticket || _busy)
        {
            return;
        }

        _list.SelectedItem = null;
        var choices = GetActions(ticket.Status);
        if (choices.Length == 0)
        {
            await DisplayAlertAsync("VALE", "Bu araç için kullanılabilir işlem yok.", "Tamam");
            return;
        }

        var choice = await DisplayActionSheetAsync(ticket.LicensePlate, "Kapat", null, choices);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Kapat")
        {
            return;
        }

        try
        {
            _busy = true;
            switch (choice)
            {
                case "Park edildi":
                    await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Parked);
                    break;
                case "Araç isteniyor":
                    await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Requested);
                    break;
                case "Nakit ile teslim et":
                    await CompleteCheckoutAsync(ticket, PaymentMethod.Cash);
                    break;
                case "Kart ile teslim et":
                    await CompleteCheckoutAsync(ticket, PaymentMethod.Card);
                    break;
                case "Havale/EFT ile teslim et":
                    await CompleteCheckoutAsync(ticket, PaymentMethod.Transfer);
                    break;
                case "Kaydı iptal et":
                    await _api.UpdateStatusAsync(ticket.Id, TicketStatus.Cancelled);
                    break;
            }

            await RefreshAfterMutationAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("İşlem başarısız", ex.Message, "Tamam");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CompleteCheckoutAsync(TicketSummaryDto ticket, PaymentMethod method)
    {
        var confirm = await DisplayAlertAsync(
            "Teslim onayı",
            $"{ticket.LicensePlate} aracı {ticket.AmountDue:N2} TL tahsilat ile teslim edilsin mi?",
            "Teslim Et",
            "Vazgeç");
        if (!confirm)
        {
            return;
        }

        var result = await _api.CheckoutAsync(ticket.Id, method);
        await DisplayAlertAsync("Teslim tamamlandı", $"Tahsilat: {result.PaidAmount:N2} TL", "Tamam");
    }

    private async Task RefreshAfterMutationAsync()
    {
        _busy = false;
        await RefreshAsync();
        _busy = true;
    }

    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        if (_loginButton is not null)
        {
            _loginButton.IsEnabled = !busy;
            _loginButton.Text = busy ? "Bağlanıyor…" : "Giriş Yap";
        }
        if (_loginActivity is not null)
        {
            _loginActivity.IsVisible = busy;
            _loginActivity.IsRunning = busy;
        }
        if (_loginStatus is not null && (!busy || _loginStatus.TextColor != Color.FromArgb("#FCA5A5")))
        {
            _loginStatus.Text = status;
            _loginStatus.TextColor = SecondaryTextColor;
        }
    }

    private static Entry CreateEntry(string placeholder, Keyboard keyboard, bool isPassword = false) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard,
        IsPassword = isPassword,
        HeightRequest = 52,
        BackgroundColor = SoftCardColor,
        TextColor = Colors.White,
        PlaceholderColor = SecondaryTextColor,
        ClearButtonVisibility = isPassword ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing
    };

    private static (Border Card, Label Value) CreateMetricCard(string title, string initialValue, string subtitle, string accentHex)
    {
        var value = new Label
        {
            Text = initialValue,
            TextColor = Colors.White,
            FontSize = 25,
            FontAttributes = FontAttributes.Bold
        };

        var card = new Border
        {
            BackgroundColor = CardColor,
            Stroke = BorderColor,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14),
            Content = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = title, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(accentHex) },
                    value,
                    new Label { Text = subtitle, FontSize = 10, TextColor = SecondaryTextColor }
                }
            }
        };

        return (card, value);
    }

    private static string[] GetActions(TicketStatus status) => status switch
    {
        TicketStatus.Received => ["Park edildi", "Kaydı iptal et"],
        TicketStatus.Parked => ["Araç isteniyor", "Kaydı iptal et"],
        TicketStatus.Requested => ["Nakit ile teslim et", "Kart ile teslim et", "Havale/EFT ile teslim et"],
        _ => []
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TicketStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is TicketStatus status
        ? status switch
        {
            TicketStatus.Received => "TESLİM ALINDI",
            TicketStatus.Parked => "PARK EDİLDİ",
            TicketStatus.Requested => "İSTENİYOR",
            TicketStatus.Delivered => "TESLİM EDİLDİ",
            TicketStatus.Cancelled => "İPTAL",
            _ => status.ToString().ToUpperInvariant()
        }
        : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class TicketStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is TicketStatus status
        ? status switch
        {
            TicketStatus.Received => Color.FromArgb("#1D4ED8"),
            TicketStatus.Parked => Color.FromArgb("#0F766E"),
            TicketStatus.Requested => Color.FromArgb("#B45309"),
            TicketStatus.Delivered => Color.FromArgb("#15803D"),
            TicketStatus.Cancelled => Color.FromArgb("#991B1B"),
            _ => Color.FromArgb("#334155")
        }
        : Color.FromArgb("#334155");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
