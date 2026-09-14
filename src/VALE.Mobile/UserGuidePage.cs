using VALE.Contracts;

namespace VALE.Mobile;

public sealed record GuideChapter(
    string Section,
    string Title,
    string Visual,
    string Summary,
    string Steps,
    string Tip);

public sealed class UserGuidePage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly IReadOnlyList<GuideChapter> _chapters;
    private readonly CarouselView _carousel;
    private readonly Label _progress = UiKit.Label(string.Empty, 12, true, true);
    private readonly Label _plan = UiKit.Label("Kullanım durumunuz kontrol ediliyor…", 12.5, true);
    private readonly Button _previous = UiKit.SecondaryButton("← Önceki");
    private readonly Button _next = UiKit.PrimaryButton("Sonraki →");

    public UserGuidePage(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;
        Title = "Görsel Kullanım Kılavuzu";
        UiKit.StylePage(this);
        _chapters = CreateChapters(user);

        var indicator = new IndicatorView
        {
            IndicatorColor = ThemeService.Palette.Border,
            SelectedIndicatorColor = ThemeService.Palette.Accent,
            HorizontalOptions = LayoutOptions.Center,
            IndicatorSize = 9
        };

        _carousel = new CarouselView
        {
            ItemsSource = _chapters,
            Loop = false,
            PeekAreaInsets = 10,
            IsBounceEnabled = true,
            IndicatorView = indicator,
            ItemTemplate = new DataTemplate(CreateChapterView)
        };
        _carousel.PositionChanged += (_, e) => UpdateNavigation(e.CurrentPosition);

        _previous.Clicked += (_, _) => Move(-1);
        _next.Clicked += (_, _) => Move(1);
        var buttons = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            ColumnSpacing = 10
        };
        buttons.Add(_previous, 0);
        buttons.Add(_next, 1);

        var quickTour = UiKit.TextButton("Ekran baloncuklarıyla hızlı turu başlat");
        quickTour.Clicked += async (_, _) => await GuidedTour.ShowMainAsync(this, _user, true);

        Content = new Grid
        {
            Padding = new Thickness(12, 14, 12, 22),
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        var root = (Grid)Content;
        root.Add(UiKit.Label("VALEM'i adım adım öğrenin", 25, true), 0, 0);
        root.Add(_plan, 0, 1);
        root.Add(_carousel, 0, 2);
        root.Add(indicator, 0, 3);
        root.Add(_progress, 0, 4);
        root.Add(new VerticalStackLayout { Spacing = 6, Children = { buttons, quickTour } }, 0, 5);
        UpdateNavigation(0);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var entitlement = await _api.GetEntitlementAsync();
            _plan.Text = entitlement.IsPremium
                ? "✓ VALEM Sınırsız hesabı • Kayıt ve tema sınırı yok"
                : $"Ücretsiz Deneme • {entitlement.RemainingVehicleRecords}/{entitlement.DemoVehicleLimit} araç hakkı kaldı";
        }
        catch { _plan.Text = "Kılavuzu çevrimdışı okuyabilirsiniz."; }
    }

    private View CreateChapterView()
    {
        var section = UiKit.Label(string.Empty, 11, true, true);
        section.SetBinding(Label.TextProperty, nameof(GuideChapter.Section));
        var title = UiKit.Label(string.Empty, 23, true);
        title.SetBinding(Label.TextProperty, nameof(GuideChapter.Title));
        var visual = new Image { HeightRequest = 190, Aspect = Aspect.AspectFill };
        visual.SetBinding(Image.SourceProperty, nameof(GuideChapter.Visual));
        var summary = UiKit.Label(string.Empty, 13, false, true);
        summary.SetBinding(Label.TextProperty, nameof(GuideChapter.Summary));
        var steps = UiKit.Label(string.Empty, 13);
        steps.SetBinding(Label.TextProperty, nameof(GuideChapter.Steps));
        var tip = UiKit.Label(string.Empty, 11.5, true);
        tip.SetBinding(Label.TextProperty, nameof(GuideChapter.Tip));

        var visualHost = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = visual
        };
        return new ScrollView
        {
            Content = UiKit.Card(new VerticalStackLayout
            {
                Spacing = 11,
                Children = { section, title, visualHost, summary, steps, UiKit.Card(tip, new Thickness(12), 12) }
            }, new Thickness(14), 20)
        };
    }

    private void Move(int delta)
    {
        var target = Math.Clamp(_carousel.Position + delta, 0, _chapters.Count - 1);
        if (target == _carousel.Position && delta > 0) return;
        _carousel.ScrollTo(target, position: ScrollToPosition.Center, animate: true);
    }

    private void UpdateNavigation(int position)
    {
        _progress.Text = $"Bölüm {position + 1} / {_chapters.Count}";
        _progress.HorizontalTextAlignment = TextAlignment.Center;
        _previous.IsEnabled = position > 0;
        _next.IsEnabled = position < _chapters.Count - 1;
        _next.Text = position == _chapters.Count - 1 ? "Kılavuz tamamlandı ✓" : "Sonraki →";
    }

    private static IReadOnlyList<GuideChapter> CreateChapters(UserDto user) =>
    [
        new("1 • BAŞLANGIÇ", "Hesap türü ve giriş", "theme_anime_sunset.jpg",
            "Kendi firmanızı oluşturabilir, kişisel hesapla deneyebilir veya firma ve şube koduyla mevcut ekibe katılabilirsiniz.",
            "1. Hesap türünü seçin.\n2. Adınızı ve e-posta adresinizi yazın.\n3. E-posta kodu veya parola yöntemini belirleyin.\n4. Personelseniz yöneticinizin verdiği firma ve şube kodlarını kullanın.",
            "Davet kodu zorunlu değildir. Firma ve şube kodları yalnızca doğru işletmenin verilerine bağlanmanızı sağlar."),
        new("2 • ANA SAYFA", "Günlük durumu izleyin", "theme_car_track.jpg",
            $"Ana sayfa {user.BranchName ?? "seçili şube"} için içerideki araçları, teslim bekleyenleri, tamamlananları ve yetkiniz varsa ciroyu gösterir.",
            "• Üst bölümden aktif şubeyi kontrol edin.\n• Özet kartlarından günün durumunu görün.\n• Son araçlardan birine dokunarak ayrıntıyı açın.\n• Yeni Araç Kabulü düğmesiyle hızlı kayıt başlatın.",
            "Birden fazla şubeye yetkiniz varsa işlem yapmadan önce seçili şubeyi kontrol edin."),
        new("3 • ARAÇ KABUL", "Yeni araç kaydı oluşturun", "theme_car_neon.jpg",
            "Hızlı kabulde yalnız plaka zorunludur. Diğer bilgileri hemen girebilir veya daha sonra araç ayrıntısından tamamlayabilirsiniz.",
            "1. Plakayı yazın.\n2. İsterseniz marka seçin; model listesi seçilen markaya göre açılır.\n3. Anahtar etiketi ve park yerini ekleyin.\n4. Mevcut hasarı not veya fotoğrafla kaydedin.\n5. Araç Kabulünü Kaydet düğmesine dokunun.",
            "Marka ve model listeleri alfabetiktir. Marka seçmeden model seçilemez."),
        new("4 • ARAÇ İŞLEMLERİ", "Teslim sürecini yönetin", "theme_car_track.jpg",
            "Araç ayrıntısı, kabulden teslime kadar yapılan işlemleri tek yerde toplar.",
            "• Araç istendiğinde durumunu Teslim İstendi yapın.\n• Araç hazır olduğunda ilgili durum adımını seçin.\n• Teslim sırasında ödeme yöntemini kontrol edin.\n• Yanlış bilgileri Düzenle bölümünden düzeltin.\n• Ödemeli veya teslim edilmiş mali kayıtlar silinemez.",
            "Ücret, giriş ve teslim zamanları üzerinden sunucuda hesaplanır; cihaz saatine güvenilmez."),
        new("5 • PROFİL VE TEMALAR", "Uygulamayı kişiselleştirin", "theme_anime_neon.jpg",
            "Profil fotoğrafınızı ve isteğe bağlı kişisel bilgilerinizi ekleyebilir; tema, vurgu rengi ve arka planı değiştirebilirsiniz.",
            "1. Ayarlar > Profilim ve Fotoğrafım bölümünü açın.\n2. Fotoğrafınızı kamera veya galeriden seçin.\n3. Telefon, doğum tarihi, şehir ve hakkımda alanlarını isteğinize göre doldurun.\n4. Görünüm seçeneklerinden temayı seçip kaydedin.",
            "Resimli temalarda yazı ve kart kontrastı otomatik ayarlanır. Galeriden arka plan ve premium çerçeveler Sınırsız pakete dahildir."),
        new("6 • DENEME VE SINIRSIZ", "Paket haklarınızı takip edin", "theme_car_neon.jpg",
            "Ücretsiz sürüm kullanıcı başına 50 araç kaydı sunar. VALEM Sınırsız, kayıt sınırını ve premium görünüm kilitlerini kaldırır.",
            "• Ana sayfadan kalan araç hakkınızı görün.\n• Ayarlar > Sürüm ve Satın Alma bölümünde paketleri karşılaştırın.\n• Satın alma Google Play tarafından tamamlanır.\n• Aynı Play ve VALEM hesabıyla Satın Almayı Geri Yükle seçeneğini kullanabilirsiniz.",
            "Ödeme veya teslim içermeyen eski bir demo kaydını silmek kotada yeniden yer açar."),
        new("7 • GÜVENLİK", "Hesabınızı koruyun", "theme_anime_sunset.jpg",
            "E-posta koduyla giriş yapabilir, güçlü parola kullanabilir ve Authenticator ile iki adımlı doğrulamayı açabilirsiniz.",
            "• Kurtarma kodlarını güvenli ve çevrimdışı bir yerde saklayın.\n• Kodları veya parolanızı personelle paylaşmayın.\n• Ortak telefonda cihazı hatırla seçeneğini kullanmayın.\n• Şüpheli durumda parolanızı değiştirip aktif oturumları kapatın.",
            "VALEM destek ekibi sizden hiçbir zaman parolanızı, giriş kodunuzu veya ödeme kartı bilginizi istemez."),
        new("8 • DESTEK", "Yardım isteyin", "theme_anime_neon.jpg",
            "Ayarlar > İletişim ve Destek bölümünden hata, kullanım sorusu veya geliştirme önerisi gönderebilirsiniz.",
            $"Destek adresi: {SupportContactPage.SupportEmail}\n\nHata bildirirken ekranı, işlemi ve yaklaşık zamanı yazın. Varsa kişisel bilgi içermeyen bir ekran görüntüsü ekleyin.",
            "Teknik hata ayrıntıları kullanıcı ekranında gösterilmez; sunucu logları destek incelemesi için ayrı tutulur.")
    ];
}
