namespace VALE.Mobile;

public sealed class ChangeEmailPage : ContentPage
{
    public ChangeEmailPage(ApiClient api)
    {
        Title = "E-posta Adresini Değiştir"; UiKit.StylePage(this);
        var email = UiKit.Entry("Yeni e-posta adresiniz", Keyboard.Email);
        var password = UiKit.Entry("Mevcut parolanız", password: true);
        var send = UiKit.PrimaryButton("Yeni Adrese Doğrulama Gönder");
        send.Clicked += async (_, _) =>
        {
            try
            {
                send.IsEnabled = false;
                await api.RequestEmailChangeAsync(email.Text?.Trim() ?? "", password.Text ?? "");
                await DisplayAlertAsync("E-postanızı kontrol edin", "Yeni adresinize gönderilen bağlantıyı onaylayın. Onaylanana kadar eski e-posta adresiniz geçerlidir.", "Tamam");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { await DisplayAlertAsync("Adres değiştirilemedi", UserMessages.For(ex), "Tamam"); }
            finally { send.IsEnabled = true; password.Text = ""; }
        };
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 14, Children = { UiKit.Label(Title, 25, true), UiKit.Label("Hesabınızın güvenliği için mevcut parolanızı doğrulayın. Adres değişikliği yeni e-posta hesabında onaylandıktan sonra tamamlanır.", 13, false, true), UiKit.Field(email), UiKit.PasswordField(password), send } } };
    }
}
