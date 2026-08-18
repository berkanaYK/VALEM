using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class NotificationCenterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<NotificationDto> _items = new();
    private readonly Label _summary = UiKit.Label("Bildirimler yükleniyor…", 12.5, false, true);
    private readonly CollectionView _list;
    private bool _busy;

    public NotificationCenterPage(ApiClient api)
    {
        _api = api;
        Title = "Bildirimler";
        UiKit.StylePage(this);

        var refresh = UiKit.SecondaryButton("Yenile");
        refresh.Clicked += async (_, _) => await RefreshAsync();
        var readAll = UiKit.TextButton("Tümünü okundu işaretle");
        readAll.Clicked += async (_, _) =>
        {
            try { await _api.MarkAllNotificationsReadAsync(); await RefreshAsync(); }
            catch (Exception ex) { await DisplayAlertAsync("Bildirimler", ex.Message, "Tamam"); }
        };

        _list = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.Single,
            EmptyView = UiKit.Label("Henüz bildiriminiz yok.", 13, false, true),
            ItemTemplate = new DataTemplate(() =>
            {
                var title = UiKit.Label("", 15, true);
                title.SetBinding(Label.TextProperty, nameof(NotificationDto.Title));
                var message = UiKit.Label("", 12.5, false, true);
                message.SetBinding(Label.TextProperty, nameof(NotificationDto.Message));
                var time = UiKit.Label("", 10.5, false, true);
                time.SetBinding(Label.TextProperty, new Binding(nameof(NotificationDto.CreatedAt), stringFormat: "{0:dd.MM.yyyy HH:mm}"));
                var dot = UiKit.Label("●", 12, true);
                dot.SetBinding(IsVisibleProperty, new Binding(nameof(NotificationDto.IsRead), converter: new InverseBoolConverter()));
                dot.TextColor = ThemeService.Palette.Accent;
                var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 7 };
                header.Add(dot, 0, 0); header.Add(title, 1, 0);
                return UiKit.Card(new VerticalStackLayout { Spacing = 5, Children = { header, message, time } }, new Thickness(12), 16);
            })
        };
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not NotificationDto item) return;
            _list.SelectedItem = null;
            if (!item.IsRead)
            {
                try { await _api.MarkNotificationReadAsync(item.Id); }
                catch { }
            }
            await DisplayAlertAsync(item.Title, item.Message, "Tamam");
            await RefreshAsync();
        };

        Content = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)
            }
        };
        var root = (Grid)Content;
        root.Add(UiKit.Label("Bildirim merkezi", 27, true), 0, 0);
        root.Add(_summary, 0, 1);
        root.Add(new HorizontalStackLayout { Spacing = 8, Children = { refresh, readAll } }, 0, 2);
        root.Add(_list, 0, 3);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        try
        {
            _busy = true;
            var result = await _api.GetNotificationsAsync();
            _summary.Text = result.UnreadCount == 0 ? "Tüm bildirimler okundu." : $"{result.UnreadCount} okunmamış bildirim";
            _items.Clear(); foreach (var item in result.Items) _items.Add(item);
        }
        catch (Exception ex) { _summary.Text = ex.Message; }
        finally { _busy = false; }
    }
}

public sealed class ApprovalCenterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly ObservableCollection<RegistrationRequestDto> _requests = new();
    private readonly ObservableCollection<CompanyInviteDto> _invites = new();
    private readonly CollectionView _requestList;
    private readonly CollectionView _inviteList;
    private bool _busy;

    public ApprovalCenterPage(ApiClient api, UserDto user)
    {
        _api = api; _user = user;
        Title = "Başvurular ve Davetler";
        UiKit.StylePage(this);

        var createInvite = UiKit.PrimaryButton("Yeni Personel Daveti Oluştur");
        createInvite.Clicked += async (_, _) => await CreateInviteAsync();
        var refresh = UiKit.SecondaryButton("Yenile");
        refresh.Clicked += async (_, _) => await RefreshAsync();

        _requestList = new CollectionView
        {
            ItemsSource = _requests,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 360,
            EmptyView = UiKit.Label("Onay bekleyen personel başvurusu yok.", 12.5, false, true),
            ItemTemplate = new DataTemplate(() =>
            {
                var name = UiKit.Label("", 15, true); name.SetBinding(Label.TextProperty, nameof(RegistrationRequestDto.FullName));
                var branch = UiKit.Label("", 12, false, true); branch.SetBinding(Label.TextProperty, new Binding(nameof(RegistrationRequestDto.BranchName), stringFormat: "Şube: {0}"));
                var email = UiKit.Label("", 11.5, false, true); email.SetBinding(Label.TextProperty, nameof(RegistrationRequestDto.Email));
                return UiKit.Card(new VerticalStackLayout { Spacing = 4, Children = { name, email, branch, UiKit.Label("Dokun: onayla / reddet", 10.5, false, true) } }, new Thickness(12), 16);
            })
        };
        _requestList.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not RegistrationRequestDto request) return;
            _requestList.SelectedItem = null;
            await ReviewAsync(request);
        };

        _inviteList = new CollectionView
        {
            ItemsSource = _invites,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 260,
            EmptyView = UiKit.Label("Henüz davet kodu oluşturulmadı.", 12.5, false, true),
            ItemTemplate = new DataTemplate(() =>
            {
                var code = UiKit.Label("", 16, true); code.SetBinding(Label.TextProperty, nameof(CompanyInviteDto.Code));
                var branch = UiKit.Label("", 11.5, false, true); branch.SetBinding(Label.TextProperty, nameof(CompanyInviteDto.BranchName));
                var expiry = UiKit.Label("", 10.5, false, true); expiry.SetBinding(Label.TextProperty, new Binding(nameof(CompanyInviteDto.ExpiresAt), stringFormat: "Son: {0:dd.MM.yyyy HH:mm}"));
                return UiKit.Card(new VerticalStackLayout { Spacing = 4, Children = { code, branch, expiry, UiKit.Label("Kopyalamak için dokunun", 10.5, false, true) } }, new Thickness(12), 16);
            })
        };
        _inviteList.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not CompanyInviteDto invite) return;
            _inviteList.SelectedItem = null;
            await Clipboard.Default.SetTextAsync(invite.Code);
            await DisplayAlertAsync("Davet kodu kopyalandı", invite.Code, "Tamam");
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 12,
                Children =
                {
                    UiKit.Label("Başvurular ve davetler", 27, true),
                    UiKit.Label("Sadece kendi firmanızdaki ve yetkiniz olan şubelerdeki başvuruları görürsünüz.", 12, false, true),
                    createInvite, refresh,
                    UiKit.Label("Onay bekleyenler", 18, true), _requestList,
                    UiKit.Label("Davet kodları", 18, true), _inviteList
                }
            }
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        try
        {
            _busy = true;
            var requests = await _api.GetRegistrationApprovalsAsync();
            var invites = await _api.GetCompanyInvitesAsync();
            _requests.Clear(); foreach (var item in requests) _requests.Add(item);
            _invites.Clear(); foreach (var item in invites) _invites.Add(item);
        }
        catch (Exception ex) { await DisplayAlertAsync("Yönetim", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }

    private async Task ReviewAsync(RegistrationRequestDto request)
    {
        var choice = await DisplayActionSheetAsync(request.FullName, "Vazgeç", null, "Onayla", "Reddet");
        if (choice == "Onayla")
        {
            var roles = CompanyAccess.AssignableRoles(_user).Where(x => !x.Equals("Owner", StringComparison.OrdinalIgnoreCase)).ToArray();
            var roleLabels = roles.Select(CompanyAccess.RoleText).ToArray();
            var selectedLabel = await DisplayActionSheetAsync("Rol seçin", "Vazgeç", null, roleLabels);
            var index = Array.IndexOf(roleLabels, selectedLabel);
            if (index < 0) return;
            try { await _api.ApproveRegistrationAsync(request.Id, roles[index]); await RefreshAsync(); }
            catch (Exception ex) { await DisplayAlertAsync("Onay başarısız", ex.Message, "Tamam"); }
        }
        else if (choice == "Reddet")
        {
            var note = await DisplayPromptAsync("Başvuruyu reddet", "İsteğe bağlı açıklama", "Reddet", "Vazgeç");
            try { await _api.RejectRegistrationAsync(request.Id, note); await RefreshAsync(); }
            catch (Exception ex) { await DisplayAlertAsync("Reddetme başarısız", ex.Message, "Tamam"); }
        }
    }

    private async Task CreateInviteAsync()
    {
        try
        {
            var branches = await _api.GetBranchesAsync();
            if (branches.Count == 0) { await DisplayAlertAsync("Şube", "Aktif şube bulunamadı.", "Tamam"); return; }
            var branchLabels = branches.Select(x => $"{x.Name} • {x.Code}").ToArray();
            var branchLabel = await DisplayActionSheetAsync("Şube seçin", "Vazgeç", null, branchLabels);
            var branchIndex = Array.IndexOf(branchLabels, branchLabel);
            if (branchIndex < 0) return;
            var roles = CompanyAccess.AssignableRoles(_user).Where(x => !x.Equals("Owner", StringComparison.OrdinalIgnoreCase)).ToArray();
            var roleLabels = roles.Select(CompanyAccess.RoleText).ToArray();
            var roleLabel = await DisplayActionSheetAsync("Rol seçin", "Vazgeç", null, roleLabels);
            var roleIndex = Array.IndexOf(roleLabels, roleLabel);
            if (roleIndex < 0) return;
            var invite = await _api.CreateCompanyInviteAsync(new CreateCompanyInviteRequest(branches[branchIndex].Id, roles[roleIndex], 7, 1));
            await Clipboard.Default.SetTextAsync(invite.Code);
            await DisplayAlertAsync("Davet oluşturuldu", $"{invite.Code}\n\nKod panoya kopyalandı ve 7 gün geçerli.", "Tamam");
            await RefreshAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Davet oluşturulamadı", ex.Message, "Tamam"); }
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is bool b && !b;
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
