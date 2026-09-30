using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed record CoachStep(string Target, string Title, string Text);
public static class GuidedTour
{
    private static UserDto? _user;
    private static bool _demo, _skipped;
    private static readonly HashSet<string> Seen = [];
    private static string Key(string page) => $"vale_coach_v2_{_user?.Id:N}_{page}";
    public static void BeginSession(UserDto user)
    {
        _user = user; _demo = user.Email == "preview@vale.invalid"; Seen.Clear();
        _skipped = !_demo && Preferences.Default.Get(Key("skipped"), false);
    }
    public static void Attach(ContentPage page)
    {
        var name = page.GetType().Name; var steps = Steps(name); if (steps.Length == 0) return;
        var runner = new CoachRunner(page, steps,
            () => name == nameof(TenantRegisterPage) ? Seen.Contains("registration") : _user is null || _skipped || Seen.Contains(name) || (!_demo && Preferences.Default.Get(Key(name), false)),
            skipped => { Seen.Add(name == nameof(TenantRegisterPage) ? "registration" : name); if (name == nameof(TenantRegisterPage)) return; if (skipped) { _skipped = true; if (!_demo) Preferences.Default.Set(Key("skipped"), true); } if (!_demo) Preferences.Default.Set(Key(name), true); });
        page.Appearing += async (_, _) => await runner.ShowAsync(); page.Disappearing += (_, _) => runner.Hide();
    }
    public static Task ShowRegistrationAsync(Page page) => Task.CompletedTask;
    public static async Task ShowMainAsync(Page page, UserDto user, bool force = false)
    {
        if (!force) return; BeginSession(user); _skipped = false;
        foreach (var name in PageNames) Preferences.Default.Remove(Key(name)); Preferences.Default.Remove(Key("skipped"));
        await page.DisplayAlertAsync("Eğitim açıldı", "Ana Sayfa, Araçlar, Raporlar ve Daha Fazla bölümlerini açtıkça ilgili alanları gösteren eğitim yeniden başlayacak.", "Tamam");
    }
    public static Task ShowPremiumAsync(Page page, UserDto user, bool force = false)
    {
        Seen.Remove(nameof(CompanyProfilePage)); Preferences.Default.Remove(Key(nameof(CompanyProfilePage)));
        return page.DisplayAlertAsync("Yeni özellikler açıldı", "Profil ve temalar sayfasını açtığınızda yeni görünüm seçenekleri alanların üzerinde gösterilecek.", "Tamam");
    }
    private static readonly string[] PageNames = [nameof(CompanyDashboardPage), nameof(CompanyTicketsPage), nameof(ReportsV31Page), nameof(NotificationsPage), nameof(MoreHubPage), nameof(CompanyProfilePage), nameof(PremiumPage)];
    private static CoachStep[] Steps(string page) => page switch
    {
        nameof(TenantRegisterPage) => [new("register-account-type", "Hesap türünü seçin", "Kişisel çalışma alanınız otomatik hazırlanır. Firma kurabilir veya firma ve şube koduyla ekibe katılabilirsiniz."), new("register-username", "Giriş bilgilerinizi oluşturun", "E-posta veya kullanıcı adıyla giriş yapabilirsiniz. Güçlü parolanızı oluşturun; e-postanıza gelen bağlantıyla adresinizi doğrulayın."), new("register-phone", "Telefon isteğe bağlı", "SMS hizmeti açıldığında Profil bölümünde numaranızı doğrulayıp SMS ile giriş yapabilirsiniz."), new("register-submit", "Ücretsiz kullanım", "50 araç kaydına kadar temel özellikler açıktır. Ömür Boyu paketinde kayıt kotası kalkar ve premium temalar açılır.")],
        nameof(CompanyDashboardPage) => [new("home-metrics", "Günlük durum", "İçerideki, teslim bekleyen ve tamamlanan araçları izleyin. Ciro yalnız yetkili hesaplara görünür."), new("home-recent", "Son araçlar", "Bir araca dokunarak ayrıntılarını ve teslim adımlarını açın."), new("home-plan-status", "Kullanım hakkınız", "Ücretsiz kullanım 50 araç kaydıdır. Satın Al / Paketim sol menüde; destek sayfası Daha Fazla bölümündedir.")],
        nameof(CompanyTicketsPage) => [new("tickets-search", "Aracı bulun", "Plaka, fiş veya telefonla arayın. Ara düğmesi sonuçları getirir."), new("tickets-closed", "Tamamlanan kayıtlar", "Kapanmış araç kayıtlarını da görmek için bu seçeneği açın."), new("tickets-list", "Araç bilgileri", "Kartlarda plaka, konum ve durum görünür. Ayrıntı ve teslim işlemleri için bir karta dokunun.")],
        nameof(ReportsV31Page) => [new("report-filters", "Tarih aralığı", "Başlangıç ve bitiş tarihini seçip raporu yenileyin."), new("report-exports", "Görüntüleme ve paylaşım", "PDF, Excel ve CSV için ayrı seçenekler vardır. Excel ve CSV önizlemesi uygulama içinde açılır.")],
        nameof(NotificationsPage) => [new("notification-actions", "Bildirimlerinizi yönetin", "Bildirimleri yenileyin veya tümünü okundu işaretleyin. Onay bekleyen personel başvuruları aşağıda görünür.")],
        nameof(MoreHubPage) => [new("more-profile", "Profiliniz", "Fotoğraf, iletişim bilgileri ve doğrulanmış e-posta değişikliğini buradan yönetin."), new("more-settings", "Ayarlar", "Açık/koyu mod, güvenlik ve yöneticiler için personel yetkilendirmesi burada."), new("more-support", "Yardım ve iletişim", "Destek sayfasından e-posta oluşturun. Görsel kılavuz bütün kullanım adımlarını anlatır.")],
        nameof(CompanyProfilePage) => [new("profile-photo", "Fotoğrafınızı ekleyin", "Kamera veya galeriden profil fotoğrafınızı seçebilirsiniz."), new("profile-appearance", "Görünümü seçin", "Sade görünüm, Gün Batımı ve Otel Girişi ücretsizdir. Diğer temalar ve çerçeveler Ömür Boyu paketinde açılır."), new("profile-header", "Sol menüyü özelleştirin", "İsim alanının arka planında profil renginizi veya hazır bir araç görselini kullanabilirsiniz.")],
        nameof(PremiumPage) => [new("plan-comparison", "Paketleri karşılaştırın", "Ömür Boyu tek seferlik satın almadır. Kayıt kotası kalkar, premium temalar ve çerçeveler açılır."), new("plan-restore", "Google Play hesabı", "Ödeme Play'in güvenli ekranında yapılır. Geri yükleme satın alma belgesini sunucuda doğrular. Mağaza kurulumu tamamlanmadan gerçek ödeme yapılamaz.")],
        _ => []
    };
}

internal sealed class CoachRunner(ContentPage page, CoachStep[] steps, Func<bool> skip, Action<bool> complete)
{
    private Grid? _root; private AbsoluteLayout? _overlay; private View? _content;
    private int _index, _generation; private bool _showing, _rendering, _pending;
    private readonly List<Action> _restore = [];
    public async Task ShowAsync()
    {
        if (_showing || _pending || skip()) return;
        _pending = true; var generation = ++_generation;
        try
        {
            await Task.Delay(500);
            if (generation != _generation || page.Content is null || page.Width <= 0 || skip() || !page.IsVisible) return;
            if (_root is null) { _content = page.Content; page.Content = null; _root = new Grid(); _root.Add(_content); page.Content = _root; }
            await Task.Delay(100);
            if (generation != _generation || _root.Width < 80 || _root.Height < 100) return;
            _showing = true; _index = 0; DisableNavigation(); await RenderAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Coach layout: {ex.GetType().Name}"); Hide(); }
        finally { if (generation == _generation) _pending = false; }
    }
    public void Hide()
    {
        _generation++; _pending = false; _showing = false; if (_overlay is not null) _root?.Children.Remove(_overlay); _overlay = null;
        foreach (var restore in _restore) restore(); _restore.Clear();
    }
    private async Task RenderAsync()
    {
        if (!_showing || _rendering || _content is null || _root is null) return; _rendering = true;
        try
        {
            if (_overlay is not null) _root.Children.Remove(_overlay);
            View? target = null;
            while (_index < steps.Length && target is null)
            {
                target = Descendants(_content).OfType<View>().FirstOrDefault(x => x.AutomationId == steps[_index].Target && x.IsVisible);
                if (target is null) _index++;
            }
            if (target is null) { complete(false); Hide(); return; }
            for (Element? parent = target.Parent; parent is not null; parent = parent.Parent)
                if (parent is ScrollView scroll) { await scroll.ScrollToAsync(target, ScrollToPosition.Center, false); break; }
            await Task.Delay(120); if (!_showing) return;
            var width = _root.Width; var height = _root.Height; var bounds = Bounds(target, _content);
            var x = Math.Clamp(bounds.X - 5, 5, width - 10); var y = Math.Clamp(bounds.Y - 5, 5, height - 10);
            bounds = new Rect(x, y, Math.Min(bounds.Width + 10, width - x - 5), Math.Min(Math.Min(bounds.Height + 10, 130), height - y - 5));
            _overlay = new AbsoluteLayout(); var blocker = new BoxView { Color = Colors.Transparent }; blocker.GestureRecognizers.Add(new TapGestureRecognizer());
            Add(blocker, new Rect(0, 0, width, height));
            var areas = new[] { new Rect(0, 0, width, bounds.Top), new Rect(0, bounds.Bottom, width, height - bounds.Bottom), new Rect(0, bounds.Top, bounds.Left, bounds.Height), new Rect(bounds.Right, bounds.Top, width - bounds.Right, bounds.Height) };
            AddBlur(areas, width, height);
            var bubbleY = bounds.Bottom + 264 < height ? bounds.Bottom + 34 : Math.Max(56, bounds.Top - 264);
            Add(new GraphicsView { Drawable = new Spotlight(areas, bounds, bubbleY), InputTransparent = true }, new Rect(0, 0, width, height));
            var next = UiKit.PrimaryButton(_index == steps.Length - 1 ? "Anladım" : "İleri"); next.AutomationId = "coach-next";
            next.Clicked += async (_, _) => { if (_rendering) return; _index++; if (_index >= steps.Length) { complete(false); Hide(); } else await RenderAsync(); };
            var card = UiKit.Card(new ScrollView { Content = new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label(steps[_index].Title, 19, true), UiKit.Label(steps[_index].Text, 13), UiKit.Label($"{_index + 1} / {steps.Length}", 11, false, true), next } } }, new Thickness(16), 18); card.AutomationId = "coach-bubble";
            Add(card, new Rect(16, bubbleY, width - 32, Math.Min(230, height - bubbleY - 10)));
            var close = new Button { Text = "×", FontSize = 28, Padding = 0, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#263244"), CornerRadius = 22, AutomationId = "coach-close" };
            SemanticProperties.SetDescription(close, "Eğitimi kapat"); close.Clicked += (_, _) => { complete(true); Hide(); };
            Add(close, new Rect(width - 56, 8, 44, 44)); _root.Add(_overlay);
        }
        finally { _rendering = false; }
    }
    private void Add(View view, Rect bounds) { AbsoluteLayout.SetLayoutBounds(view, bounds); _overlay!.Add(view); }
    private static IEnumerable<Element> Descendants(Element node)
    {
        yield return node;
        if (node is IVisualTreeElement tree) foreach (var child in tree.GetVisualChildren().OfType<Element>()) foreach (var item in Descendants(child)) yield return item;
    }
    private static Rect Bounds(View target, View root)
    {
#if ANDROID
        if (target.Handler?.PlatformView is Android.Views.View native && root.Handler?.PlatformView is Android.Views.View host)
        {
            int[] point = new int[2], origin = new int[2]; native.GetLocationOnScreen(point); host.GetLocationOnScreen(origin);
            var density = native.Resources?.DisplayMetrics?.Density ?? 1;
            return new Rect((point[0] - origin[0]) / density, (point[1] - origin[1]) / density, native.Width / density, native.Height / density);
        }
#endif
        return target.Bounds;
    }
    private void AddBlur(Rect[] areas, double width, double height)
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || _content?.Handler?.PlatformView is not Android.Views.View native || native.Width <= 0 || native.Height <= 0) return;
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap(native.Width, native.Height, Android.Graphics.Bitmap.Config.Argb8888!);
        using var canvas = new Android.Graphics.Canvas(bitmap!); native.Draw(canvas);
        using var buffer = new MemoryStream(); bitmap!.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, buffer); var bytes = buffer.ToArray();
        foreach (var area in areas.Where(x => x.Width > 0 && x.Height > 0))
        {
            var image = new Image { Source = ImageSource.FromStream(() => new MemoryStream(bytes)), Aspect = Aspect.Fill, InputTransparent = true, Clip = new RectangleGeometry { Rect = area } };
            image.HandlerChanged += (_, _) => { if (OperatingSystem.IsAndroidVersionAtLeast(31) && image.Handler?.PlatformView is Android.Views.View view) view.SetRenderEffect(Android.Graphics.RenderEffect.CreateBlurEffect(12, 12, Android.Graphics.Shader.TileMode.Clamp!)); };
            Add(image, new Rect(0, 0, width, height));
        }
#endif
    }
    private void DisableNavigation()
    {
#if ANDROID
        void Visit(Android.Views.View view, bool disable = false)
        {
            disable |= view is Google.Android.Material.BottomNavigation.BottomNavigationView or AndroidX.AppCompat.Widget.Toolbar;
            if (disable) { var enabled = view.Enabled; view.Enabled = false; _restore.Add(() => { if (view.Handle != IntPtr.Zero) view.Enabled = enabled; }); }
            if (view is Android.Views.ViewGroup group) for (var i = 0; i < group.ChildCount; i++) if (group.GetChildAt(i) is { } child) Visit(child, disable);
        }
        if (Platform.CurrentActivity?.Window?.DecorView is { } decor) Visit(decor);
#endif
    }
    private sealed class Spotlight(Rect[] areas, Rect target, double bubbleY) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Color.FromRgba(3, 8, 18, 185); foreach (var r in areas) canvas.FillRectangle((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);
            canvas.StrokeColor = Color.FromArgb("#FBBF24"); canvas.StrokeSize = 3; canvas.DrawRoundedRectangle((float)target.X, (float)target.Y, (float)target.Width, (float)target.Height, 12);
            var x = (float)target.Center.X; var end = (float)(bubbleY > target.Bottom ? target.Bottom + 3 : target.Top - 3); var start = (float)(bubbleY > target.Bottom ? bubbleY : bubbleY + 230); var direction = bubbleY > target.Bottom ? 1 : -1;
            canvas.DrawLine(x, start, x, end); canvas.DrawLine(x, end, x - 7, end + direction * 10); canvas.DrawLine(x, end, x + 7, end + direction * 10);
        }
    }
}
