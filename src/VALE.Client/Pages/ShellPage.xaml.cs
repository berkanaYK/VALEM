using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VALE.Client.Services;
using VALE.Contracts;

namespace VALE.Client.Pages;

public sealed partial class ShellPage : Page
{
    private readonly ApiClient _api;
    private readonly BranchContext _branchContext;
    private bool _isInitializingBranches;

    public ShellPage()
    {
        _api = App.Services.GetRequiredService<ApiClient>();
        _branchContext = App.Services.GetRequiredService<BranchContext>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is UserDto user)
        {
            UserNameText.Text = user.FullName;
            BranchNameText.Text = user.BranchName ?? "Şube atanmamış";
            AdministrationNavItem.Visibility = user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private async void ShellNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isInitializingBranches = true;
            var branches = await _api.GetBranchesAsync();
            BranchSelector.ItemsSource = branches;
            var selectedBranch = branches.FirstOrDefault(x => x.Id == _branchContext.ActiveBranchId)
                                 ?? branches.FirstOrDefault();
            if (selectedBranch is not null)
            {
                _branchContext.Select(selectedBranch);
                BranchSelector.SelectedItem = selectedBranch;
                BranchNameText.Text = selectedBranch.Name;
            }

            BranchSelectorPanel.Visibility = branches.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            BranchNameText.Text = exception.Message;
        }
        finally
        {
            _isInitializingBranches = false;
        }

        var firstItem = (NavigationViewItem)ShellNavigation.MenuItems[0];
        ShellNavigation.SelectedItem = firstItem;
        Navigate("dashboard", firstItem.Content?.ToString() ?? "Genel Bakış");
    }

    private void BranchSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingBranches || BranchSelector.SelectedItem is not BranchDto branch)
        {
            return;
        }

        _branchContext.Select(branch);
        BranchNameText.Text = branch.Name;
        if (ContentFrame.CurrentSourcePageType is { } currentPage)
        {
            ContentFrame.Navigate(currentPage);
        }
    }

    private void ShellNavigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        if (tag == "logout")
        {
            App.Window.SignOut();
            return;
        }

        Navigate(tag, item.Content?.ToString() ?? string.Empty);
    }

    private void Navigate(string tag, string header)
    {
        var pageType = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "tickets" => typeof(TicketsPage),
            "checkin" => typeof(CheckInPage),
            "history" => typeof(HistoryPage),
            "administration" => typeof(AdministrationPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(DashboardPage)
        };

        ShellNavigation.Header = header;
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
