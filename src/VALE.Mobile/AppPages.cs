using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class RegisterPage : ContentPage
{
    public RegisterPage(ApiClient api)
    {
        Title = "Hesap Oluştur";
        UiKit.StylePage(this);
        var name = UiKit.Entry("Ad soyad");
        var email = UiKit.Entry("E-posta", Keyboard.Email);
        var password = UiKit.Entry("Parola", password: true);
        var repeat = UiKit.Entry("Parolayı tekrar girin", password: true);
        var button = UiKit.PrimaryButton("Hesabı Oluştur");
        var activity = UiKit.Activity();
        activity.IsVisible = false;

        button.Clicked += async (_, _) =>
        {
            if (password.Text != repeat.Text)
            {
                await DisplayAlertAsync("Parola", "Parolalar eşleşmiyor.", "Tamam");
                return;
            }
            try
            {
                button.IsEnabled = false;
                activity.IsVisible = activity.IsRunning = true;
                await api.EnsureServerReadyAsync();
                var result = await api.RegisterAsync(new RegisterRequest(name.Text ?? "", email.Text ?? "", password.Text ?? ""));
                await DisplayAlertAsync("Hesap oluşturuldu", result.Message, "Tamam");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { await DisplayAlertAsync("Hesap oluşturulamadı", ex.Message, "Tamam"); }
            finally { button.IsEnabled = true; activity.IsVisible = activity.IsRunning = false; }
        };

        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 14,
            Children =
            {
                UiKit.Label("Yeni VALE hesabı", 28, true),
                UiKit.Label("Yeni hesaplar güvenlik için yönetici onayından sonra aktif olur.", 13, false, true),
                UiKit.Card(new VerticalStackLayout { Spacing = 12, Children = { name, email, password, repeat, UiKit.Label("En az 10 karakter; büyük/küçük harf, rakam ve özel karakter kullanın.", 11, false, true), button, activity } })
            }
        }};
    }
}

public sealed class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage(ApiClient api)
    {
        Title = "Şifre Sıfırla";
        UiKit.StylePage(this);
        var email = UiKit.Entry("E-posta", Keyboard.Email);
        var code = UiKit.Entry("6 haneli kod", Keyboard.Numeric);
        var newPassword = UiKit.Entry("Yeni parola", password: true);
        var send = UiKit.PrimaryButton("Kodu E-postama Gönder");
        var reset = UiKit.PrimaryButton("Yeni Parolayı Kaydet");
        code.IsVisible = newPassword.IsVisible = reset.IsVisible = false;

        send.Clicked += async (_, _) =>
        {
            try
            {
                send.IsEnabled = false;
                await api.EnsureServerReadyAsync();
                await api.RequestPasswordResetAsync(email.Text ?? "");
                code.IsVisible = newPassword.IsVisible = reset.IsVisible = true;
                await DisplayAlertAsync("Kod gönderildi", "E-posta hesabınızı kontrol edin. Kod 15 dakika geçerlidir.", "Tamam");
            }
            catch (Exception ex) { await DisplayAlertAsync("Kod gönderilemedi", ex.Message, "Tamam"); }
            finally { send.IsEnabled = true; }
        };
        reset.Clicked += async (_, _) =>
        {
            try
            {
                reset.IsEnabled = false;
                await api.ResetPasswordAsync(email.Text ?? "", code.Text ?? "", newPassword.Text ?? "");
                await DisplayAlertAsync("Parola değiştirildi", "Yeni parolanızla giriş yapabilirsiniz.", "Tamam");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { await DisplayAlertAsync("Parola değiştirilemedi", ex.Message, "Tamam"); }
            finally { reset.IsEnabled = true; }
        };

        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 14,
            Children = { UiKit.Label("Hesabınızı kurtarın", 28, true), UiKit.Label("Kayıtlı e-postanıza tek kullanımlık kod gönderilir.", 13, false, true), UiKit.Card(new VerticalStackLayout { Spacing = 12, Children = { email, send, code, newPassword, reset } }) }
        }};
    }
}

public sealed class ConnectionSettingsPage : ContentPage
{
    public ConnectionSettingsPage(ApiClient api)
    {
        Title = "Bağlantı";
        UiKit.StylePage(this);
        var custom = new Switch { IsToggled = api.CustomServerEnabled, OnColor = ThemeService.Palette.Accent };
        var url = UiKit.Entry("https://sunucu-adresi/", Keyboard.Url);
        url.Text = api.CustomServerUrl;
        url.IsVisible = custom.IsToggled;
        var status = UiKit.Label(custom.IsToggled ? "Özel sunucu kullanılıyor" : "VALE üretim sunucusu kullanılıyor", 12, false, true);
        custom.Toggled += (_, e) => { url.IsVisible = e.Value; status.Text = e.Value ? "Özel sunucu seçildi" : "VALE üretim sunucusu seçildi"; };

        var test = UiKit.SecondaryButton("Bağlantıyı Test Et");
        var save = UiKit.PrimaryButton("Kaydet");
        var reset = UiKit.SecondaryButton("Üretim Sunucusuna Dön");
        test.Clicked += async (_, _) =>
        {
            try
            {
                test.IsEnabled = false;
                status.Text = "Sunucu ve veritabanı test ediliyor…";
                await api.TestConnectionAsync(custom.IsToggled ? url.Text : ApiClient.ProductionBaseUrl);
                status.Text = "Bağlantı başarılı • sunucu ve veritabanı hazır";
            }
            catch (Exception ex) { status.Text = ex.Message; await DisplayAlertAsync("Bağlantı başarısız", ex.Message, "Tamam"); }
            finally { test.IsEnabled = true; }
        };
        save.Clicked += async (_, _) =>
        {
            try
            {
                api.ConfigureCustomServer(custom.IsToggled, url.Text);
                await DisplayAlertAsync("Kaydedildi", custom.IsToggled ? "Özel sunucu kaydedildi." : "Üretim sunucusu kullanılacak.", "Tamam");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { await DisplayAlertAsync("Adres geçersiz", ex.Message, "Tamam"); }
        };
        reset.Clicked += async (_, _) => { api.UseProductionServer(); custom.IsToggled = false; status.Text = "Üretim sunucusu seçildi"; await DisplayAlertAsync("Bağlantı sıfırlandı", "Eski/özel sunucu ayarı temizlendi.", "Tamam"); };

        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 14,
            Children =
            {
                UiKit.Label("Sunucu bağlantısı", 28, true),
                UiKit.Label("Normal kullanımda özel sunucu açmayın. Uygulama eski sürümlerde kaydedilmiş adresleri artık otomatik kullanmaz.", 13, false, true),
                UiKit.Card(new VerticalStackLayout { Spacing = 12, Children = { new HorizontalStackLayout { Spacing = 10, Children = { custom, UiKit.Label("Özel sunucu kullan", 14, true) } }, url, status, test, save, reset } })
            }
        }};
    }
}

public sealed class MainTabsPage : TabbedPage
{
    public MainTabsPage(ApiClient api, UserDto user)
    {
        Title = "VALE";
        SetDynamicResource(BarBackgroundColorProperty, "ValeCard");
        SetDynamicResource(BarTextColorProperty, "ValeText");
        SelectedTabColor = ThemeService.Palette.Accent;
        UnselectedTabColor = ThemeService.Palette.Secondary;

        Children.Add(Tab("Ana Sayfa", new DashboardPage(api, user)));
        Children.Add(Tab("Araçlar", new TicketsPage(api)));
        Children.Add(Tab("Raporlar", new ReportsPage(api)));
        Children.Add(Tab("Profil", new ProfilePage(api, user)));
        Children.Add(Tab("Ayarlar", new SettingsPage(api)));
        if (user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)) Children.Add(Tab("Yönetim", new AdminPage(api)));
    }

    private static NavigationPage Tab(string title, Page page)
    {
        page.Title = title;
        var nav = new NavigationPage(page) { Title = title };
        nav.SetDynamicResource(NavigationPage.BarBackgroundColorProperty, "ValeCard");
        nav.SetDynamicResource(NavigationPage.BarTextColorProperty, "ValeText");
        return nav;
    }
}

public sealed class DashboardPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly Label _active; private readonly Label _waiting; private readonly Label _delivered; private readonly Label _revenue;
    private readonly ObservableCollection<TicketSummaryDto> _recent = new();
    private bool _busy;

    public DashboardPage(ApiClient api, UserDto user)
    {
        _api = api; _user = user; UiKit.StylePage(this);
        var m1 = UiKit.Metric("AKTİF", "—", "Parkta"); _active = m1.Value;
        var m2 = UiKit.Metric("İSTENEN", "—", "Hazırlanıyor"); _waiting = m2.Value;
        var m3 = UiKit.Metric("BUGÜN", "—", "Teslim edilen"); _delivered = m3.Value;
        var m4 = UiKit.Metric("CİRO", "—", "Bugünkü tahsilat"); _revenue = m4.Value;
        var metrics = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }, ColumnSpacing = 10, RowSpacing = 10 };
        metrics.Add(m1.Card,0,0); metrics.Add(m2.Card,1,0); metrics.Add(m3.Card,0,1); metrics.Add(m4.Card,1,1);

        var add = UiKit.PrimaryButton("+ Yeni Araç Kabulü");
        add.Clicked += async (_, _) => await Navigation.PushAsync(new NewTicketPage(_api));
        var list = new CollectionView { ItemsSource = _recent, SelectionMode = SelectionMode.None, ItemTemplate = TicketTemplates.CardTemplate(), HeightRequest = 320, EmptyView = UiKit.Label("Aktif araç yok.", 13, false, true) };

        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 16, Spacing = 16,
            Children =
            {
                new VerticalStackLayout { Spacing = 3, Children = { UiKit.Label("Hoş geldin", 12, false, true), UiKit.Label(user.FullName, 28, true), UiKit.Label(user.BranchName ?? "Şube", 13, false, true) } },
                metrics, add, UiKit.Label("Aktif araçlar", 20, true), list
            }
        }};
    }

    protected override async void OnAppearing() { base.OnAppearing(); if (!_busy) await RefreshAsync(); }
    private async Task RefreshAsync()
    {
        try
        {
            _busy = true;
            var dashboard = await _api.GetDashboardAsync();
            _active.Text = dashboard.ActiveVehicles.ToString(); _waiting.Text = dashboard.WaitingForDelivery.ToString(); _delivered.Text = dashboard.DeliveredToday.ToString(); _revenue.Text = $"{dashboard.RevenueToday:N2} TL";
            _recent.Clear(); foreach (var ticket in dashboard.RecentTickets.Take(8)) _recent.Add(ticket);
        }
        catch (Exception ex) { await DisplayAlertAsync("Veriler alınamadı", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }
}

public sealed class TicketsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<TicketSummaryDto> _items = new();
    private readonly Entry _search; private readonly Switch _closed; private readonly CollectionView _list;
    private bool _busy;

    public TicketsPage(ApiClient api)
    {
        _api = api; UiKit.StylePage(this);
        _search = UiKit.Entry("Plaka, fiş veya telefon ara", Keyboard.Text);
        _search.Completed += async (_, _) => await RefreshAsync();
        _closed = new Switch { OnColor = ThemeService.Palette.Accent };
        _closed.Toggled += async (_, _) => await RefreshAsync();
        var searchButton = UiKit.SecondaryButton("Ara"); searchButton.Clicked += async (_, _) => await RefreshAsync();
        var add = UiKit.PrimaryButton("+ Yeni Araç"); add.Clicked += async (_, _) => await Navigation.PushAsync(new NewTicketPage(_api));
        _list = new CollectionView { ItemsSource = _items, SelectionMode = SelectionMode.Single, ItemTemplate = TicketTemplates.CardTemplate(), EmptyView = UiKit.Label("Kayıt bulunamadı.", 13, false, true) };
        _list.SelectionChanged += TicketSelected;

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            RowSpacing = 10
        };
        var root = (Grid)Content;
        root.Add(UiKit.Label("Araç yönetimi", 26, true),0,0);
        var searchGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing=8 }; searchGrid.Add(_search,0,0); searchGrid.Add(searchButton,1,0); root.Add(searchGrid,0,1);
        root.Add(new HorizontalStackLayout { Spacing=9, Children={ _closed, UiKit.Label("Teslim/iptal kayıtlarını da göster",12,false,true) } },0,2);
        root.Add(add,0,3); root.Add(_list,0,4);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }
    private async Task RefreshAsync()
    {
        if (_busy) return;
        try { _busy=true; var page=await _api.GetTicketsAsync(_search.Text,_closed.IsToggled); _items.Clear(); foreach(var x in page.Items)_items.Add(x); }
        catch(Exception ex){ await DisplayAlertAsync("Araçlar alınamadı",ex.Message,"Tamam"); }
        finally{_busy=false;}
    }

    private async void TicketSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TicketSummaryDto ticket || _busy) return;
        _list.SelectedItem=null;
        var actions=new List<string>();
        if(ticket.Status==TicketStatus.Received)actions.Add("Park edildi");
        if(ticket.Status==TicketStatus.Parked)actions.Add("Araç isteniyor");
        if(ticket.Status==TicketStatus.Requested){actions.Add("Nakit teslim");actions.Add("Kart teslim");actions.Add("Havale/EFT teslim");}
        if(ticket.Status is TicketStatus.Received or TicketStatus.Parked)actions.Add("Kaydı iptal et");
        if(actions.Count==0){await DisplayAlertAsync(ticket.LicensePlate,"Bu kayıt kapanmış.","Tamam");return;}
        var choice=await DisplayActionSheetAsync(ticket.LicensePlate,"Kapat",null,actions.ToArray()); if(string.IsNullOrWhiteSpace(choice)||choice=="Kapat")return;
        try
        {
            _busy=true;
            if(choice=="Park edildi")await _api.UpdateStatusAsync(ticket.Id,TicketStatus.Parked);
            else if(choice=="Araç isteniyor")await _api.UpdateStatusAsync(ticket.Id,TicketStatus.Requested);
            else if(choice=="Kaydı iptal et")await _api.UpdateStatusAsync(ticket.Id,TicketStatus.Cancelled);
            else
            {
                var method=choice.StartsWith("Nakit")?PaymentMethod.Cash:choice.StartsWith("Kart")?PaymentMethod.Card:PaymentMethod.Transfer;
                var confirm=await DisplayAlertAsync("Teslimi onayla",$"{ticket.LicensePlate} aracı {ticket.AmountDue:N2} TL tahsilat ile teslim edilsin mi?","Teslim Et","Vazgeç");
                if(confirm)await _api.CheckoutAsync(ticket.Id,method);
            }
            await RefreshAsync();
        }
        catch(Exception ex){await DisplayAlertAsync("İşlem başarısız",ex.Message,"Tamam");}
        finally{_busy=false;}
    }
}

public sealed class NewTicketPage : ContentPage
{
    public NewTicketPage(ApiClient api)
    {
        Title="Yeni Araç"; UiKit.StylePage(this);
        var plate=UiKit.Entry("Plaka"); var brand=UiKit.Entry("Marka"); var model=UiKit.Entry("Model"); var color=UiKit.Entry("Renk");
        var customer=UiKit.Entry("Müşteri adı"); var phone=UiKit.Entry("Telefon",Keyboard.Telephone); var key=UiKit.Entry("Anahtar etiketi"); var spot=UiKit.Entry("Park yeri"); var notes=UiKit.Entry("Not"); var rate=UiKit.Entry("Saatlik ücret (boş = varsayılan)",Keyboard.Numeric);
        var save=UiKit.PrimaryButton("Araç Kabulünü Kaydet");
        save.Clicked+=async(_,_)=>
        {
            decimal? parsedRate=null;
            if(!string.IsNullOrWhiteSpace(rate.Text)&&!decimal.TryParse(rate.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out var r)){await DisplayAlertAsync("Ücret","Saatlik ücret geçerli değil.","Tamam");return;} else if(!string.IsNullOrWhiteSpace(rate.Text))parsedRate=r;
            try
            {
                save.IsEnabled=false;
                await api.CreateTicketAsync(new CreateTicketRequest(null,(plate.Text??"").Trim().ToUpperInvariant(),N(brand.Text),N(model.Text),N(color.Text),N(customer.Text),N(phone.Text),N(key.Text),N(spot.Text),N(notes.Text),parsedRate));
                await DisplayAlertAsync("Kaydedildi","Araç başarıyla teslim alındı.","Tamam"); await Navigation.PopAsync();
            }
            catch(Exception ex){await DisplayAlertAsync("Kayıt başarısız",ex.Message,"Tamam");}
            finally{save.IsEnabled=true;}
        };
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=12,Children={UiKit.Label("Araç kabul",26,true),UiKit.Label("Temel araç ve müşteri bilgilerini girin.",13,false,true),UiKit.Card(new VerticalStackLayout{Spacing=10,Children={plate,brand,model,color,customer,phone,key,spot,notes,rate,save}})}}};
    }
    private static string? N(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class ReportsPage : ContentPage
{
    private readonly ApiClient _api; private readonly DatePicker _from; private readonly DatePicker _to;
    private readonly Label _vehicles; private readonly Label _delivered; private readonly Label _revenue; private readonly Label _average; private readonly Label _payments;
    private readonly ObservableCollection<DailyReportPointDto> _daily=new();
    public ReportsPage(ApiClient api)
    {
        _api=api; UiKit.StylePage(this);
        _from=new DatePicker{Date=DateTime.Today.AddDays(-6)}; _to=new DatePicker{Date=DateTime.Today};
        _from.SetDynamicResource(DatePicker.TextColorProperty,"ValeText"); _to.SetDynamicResource(DatePicker.TextColorProperty,"ValeText");
        var m1=UiKit.Metric("ARAÇ","—","Dönem toplamı");_vehicles=m1.Value;var m2=UiKit.Metric("TESLİM","—","Tamamlanan");_delivered=m2.Value;var m3=UiKit.Metric("CİRO","—","Tahsilat");_revenue=m3.Value;var m4=UiKit.Metric("ORTALAMA","—","Teslim başına");_average=m4.Value;
        var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)},RowDefinitions={new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto)},ColumnSpacing=10,RowSpacing=10};grid.Add(m1.Card,0,0);grid.Add(m2.Card,1,0);grid.Add(m3.Card,0,1);grid.Add(m4.Card,1,1);
        var load=UiKit.PrimaryButton("Raporu Oluştur");load.Clicked+=async(_,_)=>await LoadAsync();
        _payments=UiKit.Label("Ödeme dağılımı —",13,false,true);
        var dailyList=new CollectionView{ItemsSource=_daily,SelectionMode=SelectionMode.None,HeightRequest=300,ItemTemplate=new DataTemplate(()=>{var day=UiKit.Label("",13,true);day.SetBinding(Label.TextProperty,new Binding(nameof(DailyReportPointDto.Day),stringFormat:"{0:dd.MM.yyyy}"));var detail=UiKit.Label("",12,false,true);detail.SetBinding(Label.TextProperty,new Binding(nameof(DailyReportPointDto.Revenue),stringFormat:"Ciro: {0:N2} TL"));return UiKit.Card(new VerticalStackLayout{Spacing=3,Children={day,detail}},new Thickness(12),14);})};
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=14,Children={UiKit.Label("Raporlar",26,true),UiKit.Label("Operasyon ve tahsilat performansını tarih aralığına göre inceleyin.",13,false,true),UiKit.Card(new HorizontalStackLayout{Spacing=12,Children={new VerticalStackLayout{Children={UiKit.Label("Başlangıç",11,true,true),_from}},new VerticalStackLayout{Children={UiKit.Label("Bitiş",11,true,true),_to}}}}),load,grid,UiKit.Card(_payments),UiKit.Label("Günlük kırılım",18,true),dailyList}}};
    }
    protected override async void OnAppearing(){base.OnAppearing();if(_daily.Count==0)await LoadAsync();}
    private async Task LoadAsync()
    {
        try
        {
            var from=LocalStart(_from.Date);var to=LocalStart(_to.Date.AddDays(1)).AddTicks(-1);var report=await _api.GetReportAsync(from,to);
            _vehicles.Text=report.TotalVehicles.ToString();_delivered.Text=report.DeliveredVehicles.ToString();_revenue.Text=$"{report.Revenue:N2} TL";_average.Text=$"{report.AverageRevenuePerDelivery:N2} TL";
            _payments.Text=string.Join("  •  ",report.Payments.Select(x=>$"{PaymentText(x.Method)}: {x.Amount:N2} TL"));_daily.Clear();foreach(var d in report.Daily)_daily.Add(d);
        }
        catch(Exception ex){await DisplayAlertAsync("Rapor alınamadı",ex.Message,"Tamam");}
    }
    private static DateTimeOffset LocalStart(DateTime date){var unspecified=DateTime.SpecifyKind(date.Date,DateTimeKind.Unspecified);return new DateTimeOffset(unspecified,TimeZoneInfo.Local.GetUtcOffset(unspecified));}
    private static string PaymentText(PaymentMethod method)=>method switch{PaymentMethod.Cash=>"Nakit",PaymentMethod.Card=>"Kart",_=>"Havale"};
}

public sealed class ProfilePage : ContentPage
{
    private readonly ApiClient _api; private UserDto _user; private readonly Entry _name; private readonly Label _email; private readonly Label _branch; private readonly Label _roles;
    public ProfilePage(ApiClient api,UserDto user)
    {
        _api=api;_user=user;UiKit.StylePage(this);_name=UiKit.Entry("Ad soyad");_name.Text=user.FullName;_email=UiKit.Label(user.Email,14);_branch=UiKit.Label(user.BranchName??"—",14);_roles=UiKit.Label(string.Join(", ",user.Roles),14);
        var save=UiKit.PrimaryButton("Profili Kaydet");save.Clicked+=async(_,_)=>{try{_user=await _api.UpdateProfileAsync(_name.Text??"");await DisplayAlertAsync("Kaydedildi","Profil güncellendi.","Tamam");}catch(Exception ex){await DisplayAlertAsync("Profil",ex.Message,"Tamam");}};
        var password=UiKit.SecondaryButton("Parolayı Değiştir");password.Clicked+=async(_,_)=>await Navigation.PushAsync(new ChangePasswordPage(_api));
        var logout=UiKit.SecondaryButton("Oturumu Kapat");logout.Clicked+=(_,_)=>{_api.Logout();App.ShowLogin();};
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=14,Children={new Image{Source="vale_logo.svg",HeightRequest=84,WidthRequest=84,HorizontalOptions=LayoutOptions.Center},UiKit.Label("Profil",26,true),UiKit.Card(new VerticalStackLayout{Spacing=10,Children={UiKit.Label("Ad soyad",11,true,true),_name,UiKit.Label("E-posta",11,true,true),_email,UiKit.Label("Şube",11,true,true),_branch,UiKit.Label("Yetkiler",11,true,true),_roles,save,password,logout}})}}};
    }
    protected override async void OnAppearing(){base.OnAppearing();try{_user=await _api.GetMeAsync();_name.Text=_user.FullName;_email.Text=_user.Email;_branch.Text=_user.BranchName??"—";_roles.Text=string.Join(", ",_user.Roles);}catch{} }
}

public sealed class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ApiClient api)
    {
        Title="Parola Değiştir";UiKit.StylePage(this);var current=UiKit.Entry("Mevcut parola",password:true);var next=UiKit.Entry("Yeni parola",password:true);var repeat=UiKit.Entry("Yeni parolayı tekrar",password:true);var save=UiKit.PrimaryButton("Parolayı Güncelle");
        save.Clicked+=async(_,_)=>{if(next.Text!=repeat.Text){await DisplayAlertAsync("Parola","Yeni parolalar eşleşmiyor.","Tamam");return;}try{await api.ChangePasswordAsync(current.Text??"",next.Text??"");await DisplayAlertAsync("Başarılı","Parolanız değiştirildi.","Tamam");await Navigation.PopAsync();}catch(Exception ex){await DisplayAlertAsync("Parola",ex.Message,"Tamam");}};
        Content=new VerticalStackLayout{Padding=16,Spacing=12,Children={UiKit.Label("Güvenlik",26,true),UiKit.Card(new VerticalStackLayout{Spacing=10,Children={current,next,repeat,UiKit.Label("En az 10 karakter; büyük/küçük harf, rakam ve özel karakter.",11,false,true),save}})}};
    }
}

public sealed class SettingsPage : ContentPage
{
    public SettingsPage(ApiClient api)
    {
        UiKit.StylePage(this);var picker=new Picker{Title="Tema",ItemsSource=new[]{"Açık","Koyu"},SelectedIndex=ThemeService.CurrentMode==ValeThemeMode.Dark?1:0};picker.SetDynamicResource(Picker.TextColorProperty,"ValeText");picker.SelectedIndexChanged+=(_,_)=>ThemeService.Apply(picker.SelectedIndex==1?ValeThemeMode.Dark:ValeThemeMode.Light);
        var connection=UiKit.SecondaryButton("Sunucu ve Bağlantı Ayarları");connection.Clicked+=async(_,_)=>await Navigation.PushAsync(new ConnectionSettingsPage(api));
        var test=UiKit.SecondaryButton("Canlı Sunucuyu Test Et");test.Clicked+=async(_,_)=>{try{test.IsEnabled=false;await api.TestConnectionAsync();await DisplayAlertAsync("Bağlantı","Sunucu ve veritabanı hazır.","Tamam");}catch(Exception ex){await DisplayAlertAsync("Bağlantı",ex.Message,"Tamam");}finally{test.IsEnabled=true;}};
        var reset=UiKit.SecondaryButton("Bağlantı Ayarlarını Sıfırla");reset.Clicked+=async(_,_)=>{api.UseProductionServer();await DisplayAlertAsync("Sıfırlandı","VALE üretim sunucusu varsayılan yapıldı.","Tamam");};
        var version=$"VALE {AppInfo.Current.VersionString} • Android";
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=14,Children={UiKit.Label("Ayarlar",26,true),UiKit.Card(new VerticalStackLayout{Spacing=10,Children={UiKit.Label("Görünüm",13,true),picker,UiKit.Label("Uygulama varsayılan olarak açık temada başlar.",11,false,true)}}),UiKit.Card(new VerticalStackLayout{Spacing=10,Children={UiKit.Label("Bağlantı",13,true),connection,test,reset}}),UiKit.Card(new VerticalStackLayout{Spacing=5,Children={UiKit.Label("Hakkında",13,true),UiKit.Label(version,12,false,true),UiKit.Label("Güvenli oturum, ortak veritabanı ve çoklu şube için tasarlanmıştır.",11,false,true)}})}}};
    }
}

public sealed class AdminPage : ContentPage
{
    private readonly ApiClient _api;private readonly ObservableCollection<AdminUserDto> _users=new();private readonly CollectionView _list;
    public AdminPage(ApiClient api)
    {
        _api=api;UiKit.StylePage(this);_list=new CollectionView{ItemsSource=_users,SelectionMode=SelectionMode.Single,EmptyView=UiKit.Label("Kullanıcı bulunamadı.",13,false,true),ItemTemplate=new DataTemplate(()=>{var name=UiKit.Label("",15,true);name.SetBinding(Label.TextProperty,nameof(AdminUserDto.FullName));var email=UiKit.Label("",12,false,true);email.SetBinding(Label.TextProperty,nameof(AdminUserDto.Email));var status=UiKit.Label("",12,true,true);status.SetBinding(Label.TextProperty,nameof(AdminUserDto.StatusText));return UiKit.Card(new VerticalStackLayout{Spacing=3,Children={name,email,status}},new Thickness(13),16);})};_list.SelectionChanged+=Selected;
        Content=new Grid{Padding=16,RowDefinitions={new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)},RowSpacing=10};var root=(Grid)Content;root.Add(UiKit.Label("Kullanıcı yönetimi",26,true),0,0);root.Add(UiKit.Label("Yeni kayıtları onaylayın veya kullanıcı erişimini kapatın.",12,false,true),0,1);root.Add(_list,0,2);
    }
    protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
    private async Task LoadAsync(){try{var users=await _api.GetAdminUsersAsync();_users.Clear();foreach(var u in users)_users.Add(u);}catch(Exception ex){await DisplayAlertAsync("Kullanıcılar",ex.Message,"Tamam");}}
    private async void Selected(object? sender,SelectionChangedEventArgs e){if(e.CurrentSelection.FirstOrDefault() is not AdminUserDto user)return;_list.SelectedItem=null;var action=user.IsActive?"Erişimi Kapat":"Hesabı Onayla";var yes=await DisplayAlertAsync(user.FullName,$"{action} işlemi uygulansın mı?",action,"Vazgeç");if(!yes)return;try{await _api.SetUserActiveAsync(user.Id,!user.IsActive);await LoadAsync();}catch(Exception ex){await DisplayAlertAsync("Kullanıcı",ex.Message,"Tamam");}}
}

public static class TicketTemplates
{
    public static DataTemplate CardTemplate()=>new(()=>
    {
        var plate=UiKit.Label("",18,true);plate.SetBinding(Label.TextProperty,nameof(TicketSummaryDto.LicensePlate));var vehicle=UiKit.Label("",12,false,true);vehicle.SetBinding(Label.TextProperty,nameof(TicketSummaryDto.VehicleDescription));var status=UiKit.Label("",11,true);status.SetBinding(Label.TextProperty,new Binding(nameof(TicketSummaryDto.Status),converter:new TicketStatusTextConverter()));var amount=UiKit.Label("",13,true);amount.HorizontalTextAlignment=TextAlignment.End;amount.SetBinding(Label.TextProperty,new Binding(nameof(TicketSummaryDto.AmountDue),stringFormat:"{0:N2} TL"));
        var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)},RowDefinitions={new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto)},ColumnSpacing=8};grid.Add(new VerticalStackLayout{Spacing=2,Children={plate,vehicle}},0,0);grid.Add(status,1,0);grid.Add(amount,1,1);return UiKit.Card(grid,new Thickness(13),16);
    });
}

public sealed class TicketStatusTextConverter : IValueConverter
{
    public object Convert(object? value,Type targetType,object? parameter,CultureInfo culture)=>value is TicketStatus s?s switch{TicketStatus.Received=>"Teslim alındı",TicketStatus.Parked=>"Parkta",TicketStatus.Requested=>"İsteniyor",TicketStatus.Delivered=>"Teslim edildi",TicketStatus.Cancelled=>"İptal",_=>s.ToString()}:"";
    public object ConvertBack(object? value,Type targetType,object? parameter,CultureInfo culture)=>throw new NotSupportedException();
}
