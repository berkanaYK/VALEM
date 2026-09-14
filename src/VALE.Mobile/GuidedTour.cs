using VALE.Contracts;

namespace VALE.Mobile;

public sealed record TourStep(string Icon, string Title, string Text);

public static class GuidedTour
{
    private const string RegistrationKey = "vale_tour_registration_v1";
    private static string MainKey(Guid id) => $"vale_tour_main_v1_{id:N}";
    private static string PremiumKey(Guid id) => $"vale_tour_premium_v1_{id:N}";

    public static Task ShowRegistrationAsync(Page owner) => ShowOnceAsync(owner, RegistrationKey,
    [
        new("👤", "Hesap türünü seçin", "Kendi firmanızı kurabilir, kişisel deneme hesabı açabilir veya yöneticinizin firma ve şube koduyla ekibe katılabilirsiniz."),
        new("✉", "Temel bilgiler yeterli", "Adınızı ve e-posta adresinizi yazın. E-posta koduyla giriş seçeneğinde parola oluşturmanız gerekmez."),
        new("🏢", "Firma kodları ne işe yarar?", "Firma ve şube kodları yalnızca personeli doğru işletmenin verilerine bağlar. Kişisel deneme hesabı için davet kodu gerekmez."),
        new("★", "Ücretsiz deneme", "Her kullanıcı 50 araç kaydına kadar temel özellikleri deneyebilir. Eski kayıt silinirse kota yeniden açılır; Sınırsız paket tüm kısıtları kaldırır.")
    ]);

    public static Task ShowMainAsync(Page owner, UserDto user, bool force = false) => ShowOnceAsync(owner, MainKey(user.Id),
    [
        new("⌂", "Ana sayfa", "Günlük araç hareketlerini ve özetleri burada görürsünüz. Alt menü uygulamanın ana bölümlerine hızlı geçiş sağlar."),
        new("🚗", "Araç kabul", "Araçlar bölümündeki ekle düğmesiyle yeni kabul açın. Plaka zorunludur; marka, model, müşteri ve fotoğraf isteğe bağlıdır."),
        new("☰", "Profil ve ayarlar", "Sol menüden profil fotoğrafınızı, iletişim bilgilerinizi, görünümü ve hesap güvenliğini yönetebilirsiniz."),
        new("50", "Deneme kotası", "Ücretsiz kullanım 50 araç kaydıyla sınırlıdır. Ayarlar içindeki Sürüm ve Satın Alma sayfası kalan hakkınızı gösterir."),
        new("?", "Rehberi yeniden açabilirsiniz", "Ayarlar > Uygulama Rehberi yoluyla bu anlatımı istediğiniz zaman tekrar izleyebilirsiniz.")
    ], force);

    public static Task ShowPremiumAsync(Page owner, UserDto user, bool force = false) => ShowOnceAsync(owner, PremiumKey(user.Id),
    [
        new("∞", "Sınırsız kayıt açıldı", "Artık araç kabul sayısında sınır yok."),
        new("🎨", "Tüm temalar açık", "Profil > Görünüm alanından resimli temaları ve galerinizdeki kişisel arka planı kullanabilirsiniz."),
        new("✓", "Hesabınıza kaydedildi", "Satın alma bu VALEM hesabına bağlıdır. Uygulamayı yeniden kursanız da Satın almayı geri yükle seçeneğini kullanabilirsiniz.")
    ], force);

    private static async Task ShowOnceAsync(Page owner, string key, IReadOnlyList<TourStep> steps, bool force = false)
    {
        if (!force && Preferences.Default.Get(key, false)) return;
        await Task.Delay(350);
        if (owner.Navigation.ModalStack.Any(x => x is TourPage)) return;
        await owner.Navigation.PushModalAsync(new TourPage(steps, () => Preferences.Default.Set(key, true)));
    }
}

public sealed class TourPage : ContentPage
{
    private readonly IReadOnlyList<TourStep> _steps;
    private readonly Action _completed;
    private readonly Label _icon = UiKit.Label("", 42, true);
    private readonly Label _title = UiKit.Label("", 23, true);
    private readonly Label _text = UiKit.Label("", 14);
    private readonly Label _progress = UiKit.Label("", 11, false, true);
    private readonly Button _back = UiKit.SecondaryButton("Geri");
    private readonly Button _next = UiKit.PrimaryButton("İleri");
    private int _index;

    public TourPage(IReadOnlyList<TourStep> steps, Action completed)
    {
        _steps = steps; _completed = completed;
        BackgroundColor = Color.FromRgba(4, 10, 22, 220);
        Shell.SetNavBarIsVisible(this, false);
        _icon.HorizontalTextAlignment = TextAlignment.Center;
        _title.HorizontalTextAlignment = TextAlignment.Center;
        _text.HorizontalTextAlignment = TextAlignment.Center;
        _progress.HorizontalTextAlignment = TextAlignment.Center;
        _back.Clicked += (_, _) => { if (_index > 0) { _index--; Render(); } };
        _next.Clicked += async (_, _) => { if (_index + 1 < _steps.Count) { _index++; Render(); } else await CloseAsync(); };
        var skip = UiKit.TextButton("Şimdi geç"); skip.TextColor = Colors.White; skip.Clicked += async (_, _) => await CloseAsync();
        var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 10 };
        buttons.Add(_back, 0); buttons.Add(_next, 1);
        Content = new Grid
        {
            Padding = 22,
            Children =
            {
                new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 14,
                    Children = { UiKit.Card(new VerticalStackLayout { Spacing = 13, Children = { _icon, _title, _text, _progress, buttons, skip } }, new Thickness(22), 24) }
                }
            }
        };
        Render();
    }

    private void Render()
    {
        var step = _steps[_index];
        _icon.Text = step.Icon; _title.Text = step.Title; _text.Text = step.Text;
        _progress.Text = $"{_index + 1} / {_steps.Count}";
        _back.IsVisible = _index > 0;
        _next.Text = _index + 1 == _steps.Count ? "Anladım" : "İleri";
    }

    private async Task CloseAsync() { _completed(); await Navigation.PopModalAsync(); }
}
