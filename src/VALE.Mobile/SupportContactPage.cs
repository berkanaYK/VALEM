using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace VALE.Mobile;

public sealed class SupportContactPage : ContentPage
{
    public const string SupportEmail = "berkanaz.aydin8@gmail.com";

    public SupportContactPage()
    {
        Title = "İletişim ve Destek";
        UiKit.StylePage(this);

        var email = UiKit.Label(SupportEmail, 16, true);
        email.HorizontalTextAlignment = TextAlignment.Center;

        var send = UiKit.PrimaryButton("E-posta Gönder");
        send.AutomationId = "support-send-email";
        send.Clicked += async (_, _) => await ComposeEmailAsync(send);

        var webMail = UiKit.SecondaryButton("Tarayıcıdan E-posta Gönder");
        webMail.AutomationId = "support-web-email";
        webMail.Clicked += async (_, _) => await ComposeEmailAsync(webMail, browserOnly: true);

        var copy = UiKit.SecondaryButton("E-posta Adresini Kopyala");
        copy.AutomationId = "support-copy-email";
        copy.Clicked += async (_, _) =>
        {
            await Clipboard.Default.SetTextAsync(SupportEmail);
            await DisplayAlertAsync("Kopyalandı", "Destek e-posta adresi panoya kopyalandı.", "Tamam");
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 22, 18, 32),
                Spacing = 14,
                Children =
                {
                    new Image { Source = "vale_logo.svg", HeightRequest = 88, Aspect = Aspect.AspectFit },
                    UiKit.Label("VALEM İletişim ve Destek", 25, true),
                    UiKit.Label("Bir hata bildirmeniz, kullanım hakkında soru sormanız veya geliştirme önerisi paylaşmanız gerektiğinde doğrudan iletişime geçebilirsiniz.", 13, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            UiKit.Label("Destek e-postası", 12, true, true),
                            email,
                            send,
                            webMail,
                            UiKit.Label("E-posta uygulamanızda hesap kurulu değilse tarayıcı seçeneğini kullanabilirsiniz.", 11.5, false, true),
                            copy
                        }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            UiKit.Label("Hata bildirirken", 17, true),
                            UiKit.Label("• Hatanın görüldüğü ekranı\n• Yapmaya çalıştığınız işlemi\n• Yaklaşık tarih ve saati\n• Varsa ekran görüntüsünü", 12.5),
                            UiKit.Label("Parolanızı, e-posta giriş kodunuzu, Authenticator kodunuzu veya ödeme bilgilerinizi göndermeyin.", 11.5, true, true)
                        }
                    }),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            UiKit.Label("İstek ve öneriler", 17, true),
                            UiKit.Label("İhtiyacınızı hangi işlem sırasında duyduğunuzu ve beklediğiniz sonucu kısa bir örnekle anlatmanız, talebin daha hızlı değerlendirilmesini sağlar.", 12.5)
                        }
                    })
                }
            }
        };
    }

    private async Task ComposeEmailAsync(Button button, bool browserOnly = false)
    {
        try
        {
            button.IsEnabled = false;
            var message = new EmailMessage
            {
                Subject = "VALEM Destek Talebi",
                Body = $"Merhaba,\n\nTalebim: \n\nKarşılaştığım ekran: \nTarih ve saat: \n\nUygulama sürümü: {AppInfo.Current.VersionString}\nAndroid sürümü: {DeviceInfo.Current.VersionString}",
                To = [SupportEmail]
            };
            if (browserOnly)
            {
                await OpenWebMailAsync(message);
                return;
            }
            try
            {
#if ANDROID
                // SENDTO restricts the resolver to mail apps; Android owns default/one-time selection.
                var uri = Android.Net.Uri.Parse($"mailto:{SupportEmail}?subject={Uri.EscapeDataString(message.Subject)}&body={Uri.EscapeDataString(message.Body)}");
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionSendto, uri);
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity is null) throw new FeatureNotSupportedException();
                activity.StartActivity(intent);
#else
                await Email.Default.ComposeAsync(message);
#endif
            }
            catch (Exception ex) when (ex is FeatureNotSupportedException
#if ANDROID
                or Android.Content.ActivityNotFoundException
#endif
            )
            {
                await OpenWebMailAsync(message);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("E-posta açılamadı", UserMessages.For(ex), "Tamam");
        }
        finally { button.IsEnabled = true; }
    }

    private static Task OpenWebMailAsync(EmailMessage message)
    {
        var url = $"https://mail.google.com/mail/?view=cm&fs=1&to={Uri.EscapeDataString(SupportEmail)}&su={Uri.EscapeDataString(message.Subject ?? "")}&body={Uri.EscapeDataString(message.Body ?? "")}";
        return Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
    }
}
