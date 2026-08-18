using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ApiClient : IDisposable
{
    private const string CustomEnabledPreference = "vale_custom_server_v2_enabled";
    private const string CustomUrlPreference = "vale_custom_server_v2_url";
    public const string ProductionBaseUrl = "https://vale-api-5fvb.onrender.com/";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private HttpClient _httpClient;
    private string? _accessToken;

    public ApiClient() => _httpClient = CreateHttpClient(EffectiveBaseUrl);
    public bool CustomServerEnabled => Preferences.Default.Get(CustomEnabledPreference, false);
    public string CustomServerUrl => Preferences.Default.Get(CustomUrlPreference, ProductionBaseUrl);
    public string EffectiveBaseUrl => CustomServerEnabled ? NormalizeBaseUrl(CustomServerUrl) : ProductionBaseUrl;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public void UseProductionServer()
    {
        Preferences.Default.Set(CustomEnabledPreference, false);
        RebuildClient(ProductionBaseUrl);
    }

    public void ConfigureCustomServer(bool enabled, string? url)
    {
        if (!enabled) { UseProductionServer(); return; }
        var normalized = NormalizeBaseUrl(url ?? string.Empty);
        Preferences.Default.Set(CustomUrlPreference, normalized);
        Preferences.Default.Set(CustomEnabledPreference, true);
        RebuildClient(normalized);
    }

    public async Task EnsureServerReadyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new InvalidOperationException("İnternet bağlantısı bulunamadı. Wi‑Fi veya mobil veriyi kontrol edin.");

        var watch = Stopwatch.StartNew();
        var attempt = 0;
        Exception? lastError = null;
        while (watch.Elapsed < TimeSpan.FromSeconds(75))
        {
            attempt++;
            progress?.Report(attempt == 1 ? "Sunucu ve veritabanı kontrol ediliyor…" : $"Sunucu hazırlanıyor… {Math.Min(95, (int)(watch.Elapsed.TotalSeconds / 75d * 100d))}%");
            try
            {
                using var response = await SendWithTimeoutAsync(() => CreateRequest(HttpMethod.Get, "health/ready"), TimeSpan.FromSeconds(8), ct);
                if (response.IsSuccessStatusCode) { progress?.Report("Sunucu hazır • güvenli bağlantı kuruldu"); return; }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { lastError = ex; }
            await Task.Delay(TimeSpan.FromSeconds(2.5), ct);
        }
        throw new InvalidOperationException("VALE sunucusu hazır hale gelmedi. Üretim sunucusunu kullanıyorsanız tekrar deneyin; özel sunucu açıksa Bağlantı Ayarları'ndan adresi kontrol edin.", lastError);
    }

    public async Task TestConnectionAsync(string? overrideUrl = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(overrideUrl) ? EffectiveBaseUrl : NormalizeBaseUrl(overrideUrl);
        using var client = CreateHttpClient(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, "health/ready");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Sunucu hazır değil: {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("E-posta ve parola alanlarını doldurun.");
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login", new LoginRequest(email.Trim(), password), false, ct);
        await EnsureSuccessAsync(response, ct);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct) ?? throw new InvalidOperationException("Sunucudan geçerli giriş yanıtı alınamadı.");
        _accessToken = login.AccessToken;
        return login;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/register", request, false, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions, ct) ?? throw new InvalidOperationException("Hesap oluşturma yanıtı alınamadı.");
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/forgot-password", new ForgotPasswordRequest(email.Trim()), false, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/reset-password", new ResetPasswordRequest(email.Trim(), code.Trim(), newPassword), false, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<UserDto> GetMeAsync(CancellationToken ct = default) => GetAsync<UserDto>("api/auth/me", true, ct);
    public async Task<UserDto> UpdateProfileAsync(string fullName, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/auth/me", new UpdateProfileRequest(fullName.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Profil yanıtı alınamadı.");
    }
    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword), true, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) => GetAsync<DashboardDto>("api/dashboard", true, ct);
    public Task<PagedResponse<TicketSummaryDto>> GetTicketsAsync(string? search = null, bool includeClosed = false, CancellationToken ct = default)
    {
        var path = $"api/tickets?page=1&pageSize=100&includeClosed={includeClosed.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(search)) path += "&search=" + Uri.EscapeDataString(search.Trim());
        return GetAsync<PagedResponse<TicketSummaryDto>>(path, true, ct);
    }
    public Task<TicketDetailDto> GetTicketDetailAsync(Guid id, CancellationToken ct = default) => GetAsync<TicketDetailDto>($"api/tickets/{id}", true, ct);

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/tickets", request, true, ct, TimeSpan.FromSeconds(45));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Araç kaydı oluşturulamadı.");
    }
    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid id, TicketStatus status, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Patch, $"api/tickets/{id}/status", new UpdateTicketStatusRequest(status), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Araç durumu güncellenemedi.");
    }
    public async Task<CheckoutResponse> CheckoutAsync(Guid id, PaymentMethod method, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, $"api/tickets/{id}/checkout", new CheckoutTicketRequest(method), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions, ct) ?? throw new InvalidOperationException("Teslim işlemi tamamlanamadı.");
    }
    public Task<ReportSummaryDto> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        GetAsync<ReportSummaryDto>($"api/reports/summary?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", true, ct);
    public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<AdminUserDto>>("api/admin/users", true, ct);
    public async Task<AdminUserDto> SetUserActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Patch, $"api/admin/users/{id}/status", new UpdateUserStatusRequest(active), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Kullanıcı durumu güncellenemedi.");
    }

    public void Logout() => _accessToken = null;

    private async Task<T> GetAsync<T>(string path, bool authorized, CancellationToken ct)
    {
        using var response = await SendWithTimeoutAsync(() => CreateRequest(HttpMethod.Get, path, authorized), TimeSpan.FromSeconds(25), ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct) ?? throw new InvalidOperationException("Sunucudan geçerli veri alınamadı.");
    }

    private Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string path, T value, bool authorized, CancellationToken ct, TimeSpan? timeout = null)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return SendWithTimeoutAsync(() =>
        {
            var request = CreateRequest(method, path, authorized);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }, timeout ?? TimeSpan.FromSeconds(25), ct);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool authorized = false)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (authorized)
        {
            if (string.IsNullOrWhiteSpace(_accessToken)) throw new InvalidOperationException("Oturum açılmamış. Tekrar giriş yapın.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(Func<HttpRequestMessage> factory, TimeSpan timeout, CancellationToken ct)
    {
        using var request = factory();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        try { return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("Sunucu yanıt vermedi. Bağlantı zaman aşımına uğradı."); }
        catch (HttpRequestException ex) { throw new InvalidOperationException($"VALE sunucusuna bağlanılamadı. {GetDeepestMessage(ex)}", ex); }
        catch (Exception ex) when (ex.GetType().FullName?.StartsWith("Java.", StringComparison.Ordinal) == true) { throw new InvalidOperationException($"Android ağ katmanı hatası: {GetDeepestMessage(ex)}", ex); }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await ReadProblemDetailAsync(response, ct);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => detail ?? "E-posta/parola hatalı veya hesabınız henüz onaylanmamış.",
            HttpStatusCode.Forbidden => "Bu işlem için yetkiniz yok.",
            HttpStatusCode.Conflict => detail ?? "Bu bilgi daha önce kullanılmış.",
            HttpStatusCode.TooManyRequests => "Çok fazla deneme yapıldı. Güvenlik nedeniyle kısa bir süre sonra tekrar deneyin.",
            HttpStatusCode.RequestEntityTooLarge => detail ?? "Gönderilen veri çok büyük.",
            HttpStatusCode.ServiceUnavailable => detail ?? "Sunucu veya bağlı servis şu anda hazır değil.",
            _ => detail ?? $"Sunucu hatası: {(int)response.StatusCode} {response.ReasonPhrase}"
        };
        throw new InvalidOperationException(message);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) return detail.GetString();
            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) return message.GetString();
            if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String) return title.GetString();
        }
        catch (JsonException) { }
        return body.Length <= 300 ? body : body[..300];
    }

    private void RebuildClient(string baseUrl)
    {
        var old = _httpClient;
        _httpClient = CreateHttpClient(baseUrl);
        _accessToken = null;
        old.Dispose();
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(NormalizeBaseUrl(baseUrl)),
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Geçerli bir http/https sunucu adresi girin.");
        var builder = new UriBuilder(uri);
        if (!builder.Path.EndsWith('/')) builder.Path += "/";
        return builder.Uri.AbsoluteUri;
    }

    private static string GetDeepestMessage(Exception exception)
    {
        var messages = new List<string>();
        Exception? current = exception;
        while (current is not null && messages.Count < 5)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) && !messages.Contains(current.Message, StringComparer.Ordinal)) messages.Add(current.Message.Trim());
            current = current.InnerException;
        }
        return messages.Count == 0 ? "Bilinmeyen ağ hatası." : string.Join(" → ", messages);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public void Dispose() => _httpClient.Dispose();
}
