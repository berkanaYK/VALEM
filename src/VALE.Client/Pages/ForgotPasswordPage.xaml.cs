using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.Services;

namespace VALE.Client.Pages;

public sealed partial class ForgotPasswordPage : Page
{
    private readonly ApiClient _api;

    public ForgotPasswordPage()
    {
        _api = App.Services.GetRequiredService<ApiClient>();
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(LoginPage));
        }
    }

    private async void SendCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            await _api.RequestPasswordResetAsync(EmailInput.Text);
            ResetPanel.Visibility = Visibility.Visible;
            ShowStatus("Kod gönderildi. E-posta hesabınızı kontrol edin.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            await _api.ResetPasswordAsync(EmailInput.Text, CodeInput.Text, NewPasswordInput.Password);
            ShowStatus("Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        SendCodeButton.IsEnabled = !busy;
        ResetButton.IsEnabled = !busy;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
