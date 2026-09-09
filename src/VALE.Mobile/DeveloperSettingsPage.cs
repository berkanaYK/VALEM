namespace VALE.Mobile;

public sealed class DeveloperSettingsPage : ContentPage
{
    public DeveloperSettingsPage(ApiClient api)
    {
        Title = "Geliştirici";
        UiKit.StylePage(this);
        var logo = UiKit.Label("VALEM", 34, true);
        logo.HorizontalTextAlignment = TextAlignment.Center;
        logo.AutomationId = "developer-logo";
        var open = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        var opening = false;
        open.Tapped += async (_, _) =>
        {
            if (opening) return;
            try
            {
                opening = true;
                var uri = new Uri(new Uri(api.EffectiveBaseUrl), "platform-admin/database");
                if (uri.Scheme != Uri.UriSchemeHttps)
                {
                    await DisplayAlertAsync("Güvenli bağlantı gerekli", "Geliştirici girişi için HTTPS kullanan sunucu adresini seçin.", "Tamam");
                    return;
                }
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception) { await DisplayAlertAsync("Panel açılamadı", "Tarayıcı ve internet bağlantınızı kontrol edip tekrar deneyin.", "Tamam"); }
            finally { opening = false; }
        };
        logo.GestureRecognizers.Add(open);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 18, Children =
        {
            logo,
            UiKit.Label("Teknik bilgiler", 23, true),
            UiKit.Label($"Uygulama sürümü: {AppInfo.Current.VersionString}\nYapı: {AppInfo.Current.BuildString}", 13),
            UiKit.Label("Sorun bildirirken uygulama sürümünü ve hata ekranındaki destek kayıt numarasını paylaşın. Parolanızı veya doğrulama kodunuzu paylaşmayın.", 13, false, true)
        } } };
    }
}
