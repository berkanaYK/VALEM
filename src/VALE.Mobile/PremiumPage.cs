using System.Security.Cryptography;
using System.Text;
using Plugin.InAppBilling;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class PremiumPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly Label _status = UiKit.Label("Paket bilgisi alınıyor…", 14, true);
    private readonly Label _usage = UiKit.Label(string.Empty, 12.5, false, true);
    private readonly Button _purchase = UiKit.PrimaryButton("VALEM Sınırsız'ı satın al • $5.99");
    private readonly Button _restore = UiKit.SecondaryButton("Satın almayı geri yükle");
    private EntitlementDto? _entitlement;

    public PremiumPage(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;
        Title = "VALEM Sınırsız";
        UiKit.StylePage(this);
        _purchase.Clicked += async (_, _) => await PurchaseAsync();
        _restore.Clicked += async (_, _) => await RestoreAsync();

        var comparison = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            ColumnSpacing = 10
        };
        comparison.Add(PlanCard("Ücretsiz Deneme", ["50 araç kaydı", "Temel görünüm", "Standart profil"]), 0);
        comparison.Add(PlanCard("VALEM Sınırsız", ["Sınırsız araç kaydı", "Tüm resimli temalar", "Kişisel arka plan", "Premium profil görünümü", "Yeni premium özellikler"]), 1);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 20, 16, 30),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("İşiniz büyürken VALEM de sizinle büyüsün", 26, true),
                    UiKit.Label("Ücretsiz sürümü 50 araç kaydına kadar kullanın. Sınırsız paket tek seferlik satın almadır ve VALEM hesabınıza bağlanır.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 5, Children = { _status, _usage } }),
                    comparison,
                    _purchase,
                    _restore,
                    UiKit.Label("Fiyat ve ödeme Google Play tarafından gösterilir. Satın almanız uygulamayı silip yeniden kursanız da aynı Google Play ve VALEM hesabıyla geri yüklenebilir.", 10.5, false, true)
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _entitlement = await _api.GetEntitlementAsync();
            var storePrice = await TryGetStorePriceAsync();
            _purchase.Text = $"VALEM Sınırsız'ı satın al • {storePrice ?? _entitlement.DisplayPrice}";
            ApplyState();
        }
        catch (Exception ex)
        {
            _status.Text = "Paket bilgisi şu anda alınamıyor";
            _usage.Text = UserMessages.For(ex);
        }
    }

    private void ApplyState()
    {
        if (_entitlement is null) return;
        _status.Text = _entitlement.IsPremium ? "✓ Satın alındı • Tüm özellikler açık" : _entitlement.StatusText;
        _usage.Text = _entitlement.IsPremium
            ? "Bu VALEM hesabında kayıt ve premium görünüm sınırı yok."
            : $"{_entitlement.UsedVehicleRecords}/{_entitlement.DemoVehicleLimit} araç hakkı kullanıldı. Eski bir kaydı silerseniz yeniden yer açılır.";
        _purchase.IsVisible = !_entitlement.IsPremium;
        _restore.IsVisible = !_entitlement.IsPremium;
        PremiumState.Set(_user.Id, _entitlement.IsPremium);
    }

    private async Task PurchaseAsync()
    {
        try
        {
            SetBusy(true);
            var billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync()) throw new UserFacingException("Google Play ödeme hizmetine bağlanılamadı. Uygulamayı Google Play test veya mağaza sürümünden açıp tekrar deneyin.");
            try
            {
                var purchase = await billing.PurchaseAsync(PremiumProduct.Id, ItemType.InAppPurchase, AccountBinding(_user.Id));
                if (purchase is null) return;
                await VerifyAsync(purchase);
            }
            finally { await billing.DisconnectAsync(); }
        }
        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled) { }
        catch (Exception ex) { await DisplayAlertAsync("Satın alma tamamlanamadı", UserMessages.For(ex), "Tamam"); }
        finally { SetBusy(false); }
    }

    private async Task RestoreAsync()
    {
        try
        {
            SetBusy(true);
            var billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync()) throw new UserFacingException("Google Play ödeme hizmetine bağlanılamadı.");
            try
            {
                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
                var purchase = purchases?.FirstOrDefault(x => x.ProductId == PremiumProduct.Id && x.State == PurchaseState.Purchased);
                if (purchase is null)
                {
                    await DisplayAlertAsync("Satın alma bulunamadı", "Bu Google Play hesabında VALEM Sınırsız satın alması bulunamadı.", "Tamam");
                    return;
                }
                await VerifyAsync(purchase);
            }
            finally { await billing.DisconnectAsync(); }
        }
        catch (Exception ex) { await DisplayAlertAsync("Geri yükleme tamamlanamadı", UserMessages.For(ex), "Tamam"); }
        finally { SetBusy(false); }
    }

    private async Task VerifyAsync(InAppBillingPurchase purchase)
    {
        var token = string.IsNullOrWhiteSpace(purchase.PurchaseToken) ? purchase.TransactionIdentifier : purchase.PurchaseToken;
        if (string.IsNullOrWhiteSpace(token)) throw new UserFacingException("Google Play satın alma belgesi alınamadı.");
        var result = await _api.VerifyGooglePlayPurchaseAsync(PremiumProduct.Id, token);
        if (result.ShouldAcknowledge) await CrossInAppBilling.Current.FinalizePurchaseAsync([token]);
        _entitlement = result.Entitlement;
        ApplyState();
        await DisplayAlertAsync("VALEM Sınırsız açıldı", "Tüm araç kayıtları, temalar ve premium profil seçenekleri bu hesabınızda açıldı.", "Harika");
        await GuidedTour.ShowPremiumAsync(this, _user, true);
    }

    private static async Task<string?> TryGetStorePriceAsync()
    {
        try
        {
            var billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync()) return null;
            try { return (await billing.GetProductInfoAsync(ItemType.InAppPurchase, [PremiumProduct.Id]))?.FirstOrDefault()?.LocalizedPrice; }
            finally { await billing.DisconnectAsync(); }
        }
        catch { return null; }
    }

    private void SetBusy(bool busy) { _purchase.IsEnabled = !busy; _restore.IsEnabled = !busy; }
    private static string AccountBinding(Guid userId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D")))).ToLowerInvariant();
    private static View PlanCard(string title, IReadOnlyList<string> items) => UiKit.Card(new VerticalStackLayout
    {
        Spacing = 7,
        Children = { UiKit.Label(title, 15, true), UiKit.Label(string.Join("\n", items.Select(x => "✓ " + x)), 11.5) }
    }, new Thickness(11), 14);
}

public static class PremiumState
{
    private static readonly Dictionary<Guid, bool> States = [];
    public static bool IsPremium(Guid userId) => States.TryGetValue(userId, out var value) && value;
    public static void Set(Guid userId, bool value) => States[userId] = value;
}
