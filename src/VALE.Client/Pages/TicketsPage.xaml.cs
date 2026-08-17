using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;
using VALE.Contracts;

namespace VALE.Client.Pages;

public sealed partial class TicketsPage : Page
{
    public TicketsViewModel ViewModel { get; }

    public TicketsPage()
    {
        ViewModel = App.Services.GetRequiredService<TicketsViewModel>();
        InitializeComponent();
    }

    private async void TicketsPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        await ViewModel.LoadAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void ParkButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.UpdateStatusAsync(TicketStatus.Parked);

    private async void RequestButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.UpdateStatusAsync(TicketStatus.Requested);

    private async void CheckoutButton_Click(object sender, RoutedEventArgs e)
    {
        var paymentSelector = new ComboBox
        {
            Header = "Ödeme yöntemi",
            ItemsSource = new[] { "Nakit", "Kart", "Havale / EFT" },
            SelectedIndex = 0,
            MinWidth = 260
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Aracı teslim et",
            Content = paymentSelector,
            PrimaryButtonText = "Ödemeyi al ve teslim et",
            CloseButtonText = "Vazgeç",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var method = paymentSelector.SelectedIndex switch
        {
            1 => PaymentMethod.Card,
            2 => PaymentMethod.Transfer,
            _ => PaymentMethod.Cash
        };
        await ViewModel.CheckoutAsync(method);
    }
}
