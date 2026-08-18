using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class NotificationsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly VerticalStackLayout _notifications = new() { Spacing = 9 };
    private readonly VerticalStackLayout _approvals = new() { Spacing = 9 };
    private readonly Label _status = UiKit.Label("Bildirimler yükleniyor…", 11.5, false, true);
    private bool _loading;

    public NotificationsPage(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;
        Title = "Bildirimler";
        UiKit.StylePage(this);

        var refresh = UiKit.SecondaryButton("Yenile");
        refresh.Clicked += async (_, _) => await LoadAsync();
        var readAll = UiKit.SecondaryButton("Tümünü Okundu İşaretle");
        readAll.Clicked += async (_, _) =>
        {
            try { await _api.ReadAllNotificationsAsync(); await LoadAsync(); }
            catch (Exception ex) { await DisplayAlertAsync("Bildirimler", ex.Message, "Tamam"); }
        };

        var root = new VerticalStackLayout
        {
            Padding = new Thickness(16, 18, 16, 28),
            Spacing = 14,
            Children =
            {
                UiKit.Label("Bildirimler", 27, true),
                UiKit.Label("Personel başvuruları, araç teslim istekleri ve hesap bildirimleri burada görünür.", 12.5, false, true),
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 8,
                    Children = { }
                },
                _status,
                _approvals,
                _notifications
            }
        };
        var buttons = (Grid)root.Children[2];
        buttons.Add(refresh, 0, 0);
        buttons.Add(readAll, 1, 0);
        Content = new ScrollView { Content = root };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        try
        {
            _loading = true;
            _status.Text = "Güncelleniyor…";
            _notifications.Clear();
            _approvals.Clear();

            if (CompanyAccess.CanManageUsers(_user))
            {
                var pending = await _api.GetPendingRegistrationsAsync();
                if (pending.Count > 0)
                {
                    _approvals.Add(UiKit.Label($"Onay bekleyenler ({pending.Count})", 18, true));
                    foreach (var request in pending) _approvals.Add(CreateApprovalCard(request));
                }
            }

            var notifications = await _api.GetNotificationsAsync();
            if (notifications.Count == 0)
                _notifications.Add(UiKit.Card(UiKit.Label("Henüz bildiriminiz yok.", 13, false, true)));
            else
                foreach (var item in notifications) _notifications.Add(CreateNotificationCard(item));

            _status.Text = $"{notifications.Count(x => !x.IsRead)} okunmamış • {notifications.Count} toplam";
        }
        catch (Exception ex)
        {
            _status.Text = "Bildirimler yüklenemedi.";
            await DisplayAlertAsync("Bildirimler", ex.Message, "Tamam");
        }
        finally { _loading = false; }
    }

    private View CreateNotificationCard(NotificationDto item)
    {
        var title = UiKit.Label(item.IsRead ? item.Title : $"● {item.Title}", 15, true);
        var body = UiKit.Label(item.Body, 12.5, false, true);
        var time = UiKit.Label(item.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 10.5, false, true);
        var stack = new VerticalStackLayout { Spacing = 5, Children = { title, body, time } };
        if (!item.IsRead)
        {
            var read = UiKit.TextButton("Okundu işaretle");
            read.Clicked += async (_, _) =>
            {
                try { await _api.SetNotificationReadAsync(item.Id); await LoadAsync(); }
                catch (Exception ex) { await DisplayAlertAsync("Bildirim", ex.Message, "Tamam"); }
            };
            stack.Add(read);
        }
        return UiKit.Card(stack, new Thickness(13), 16);
    }

    private View CreateApprovalCard(RegistrationRequestDto request)
    {
        var approve = UiKit.PrimaryButton("Onayla");
        var reject = UiKit.TextButton("Reddet");
        reject.TextColor = ThemeService.Palette.Danger;
        approve.Clicked += async (_, _) => await DecideAsync(request, true);
        reject.Clicked += async (_, _) => await DecideAsync(request, false);

        var buttons = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        buttons.Add(approve, 0, 0);
        buttons.Add(reject, 1, 0);
        return UiKit.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                UiKit.Label(request.FullName, 16, true),
                UiKit.Label(request.Email, 11.5, false, true),
                UiKit.Label($"{request.BranchName} • {request.RequestedRole}", 12, false, true),
                UiKit.Label($"Başvuru: {request.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}", 10.5, false, true),
                buttons
            }
        }, new Thickness(13), 16);
    }

    private async Task DecideAsync(RegistrationRequestDto request, bool approve)
    {
        string? note = null;
        if (!approve)
            note = await DisplayPromptAsync("Başvuruyu reddet", "İsterseniz kısa bir açıklama yazın.", "Reddet", "Vazgeç", maxLength: 500);
        if (!approve && note is null) return;
        if (approve)
        {
            var ok = await DisplayAlertAsync("Personeli onayla", $"{request.FullName} hesabı {request.BranchName} şubesinde aktif edilsin mi?", "Onayla", "Vazgeç");
            if (!ok) return;
        }
        try
        {
            await _api.DecideRegistrationAsync(request.Id, approve, note);
            await LoadAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Başvuru", ex.Message, "Tamam"); }
    }
}
