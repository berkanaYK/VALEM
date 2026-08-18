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

public sealed class TwoFactorRequiredException : InvalidOperationException
{
    public TwoFactorRequiredException() : base("İki adımlı doğrulama kodu gerekli.") { }
}

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
            throw new InvalidOperationException("İnternet bağlantısı yok. Wi‑Fi veya mobil veriyi kontrol edin.");

        var watch = Stopwatch.StartNew();
        var attempt = 0;
        Exception? lastError = null;
        while (watch.Elapsed < TimeSpan.FromSeconds(75))
        {
            attempt++;
            progress?.Report(attempt == 1 ? "Bağlantı kontrol ediliyor…" : $"Sunucu hazırlanıyor… %{Math.Min(95, (int)(watch.Elapsed.TotalSeconds / 75d * 100d))}");
            try
            {
                using var response = await SendWithTimeoutAsync(() => CreateRequest(HttpMethod.Get, "health/ready"), TimeSpan.FromSeconds(8), ct);
                if (response.IsSuccessStatusCode) { progress?.Report("Bağlantı hazır"); return; }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { lastError = ex; }
            await Task.Delay(TimeSpan.FromSeconds(2.5), ct);
        }
        throw new InvalidOperationException("VALE sunucusuna şu anda ulaşılamıyor. Birkaç dakika sonra tekrar deneyin.", lastError);
    }

    public async Task TestConnectionAsync(string? overrideUrl = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(overrideUrl) ? EffectiveBaseUrl : NormalizeBaseUrl(overrideUrl);
        using var client = CreateHttpClient(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, "health/ready");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Sunucu henüz hazır değil. Tekrar deneyin.");
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("E-posta ve parola alanlarını doldurun.");
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login", new LoginRequest(email.Trim(), password), false, ct);
        if (await IsTwoFactorRequiredAsync(response, ct)) throw new TwoFactorRequiredException();
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
    }

    public async Task<LoginResponse> LoginWithTwoFactorAsync(string email, string password, string code, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login/2fa", new TwoFactorLoginRequest(email.Trim(), password, code.Trim()), false, ct);
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
    }

    public async Task RequestEmailLoginCodeAsync(string email, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/email-code/request", new EmailCodeRequest(email.Trim()), false, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<LoginResponse> VerifyEmailLoginCodeAsync(string email, string code, string? twoFactorCode = null, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/email-code/verify", new EmailCodeVerifyRequest(email.Trim(), code.Trim(), string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim()), false, ct);
        if (await IsTwoFactorRequiredAsync(response, ct)) throw new TwoFactorRequiredException();
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
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

    public Task<AccountProfileDto> GetAccountProfileAsync(CancellationToken ct = default) => GetAsync<AccountProfileDto>("api/auth/profile", true, ct);

    public async Task<AccountProfileDto> UpdateAccountProfileAsync(UpdateAccountProfileRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/auth/profile", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AccountProfileDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Profil kaydedilemedi.");
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword), true, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<TwoFactorStatusDto> GetTwoFactorStatusAsync(CancellationToken ct = default) => GetAsync<TwoFactorStatusDto>("api/auth/2fa/status", true, ct);

    public async Task<TwoFactorSetupDto> SetupTwoFactorAsync(CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/2fa/setup", new { }, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TwoFactorSetupDto>(JsonOptions, ct) ?? throw new InvalidOperationException("2FA kurulumu başlatılamadı.");
    }

    public async Task<TwoFactorRecoveryCodesDto> EnableTwoFactorAsync(string code, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/2fa/enable", new TwoFactorCodeRequest(code.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>(JsonOptions, ct) ?? throw new InvalidOperationException("2FA etkinleştirilemedi.");
    }

    public async Task DisableTwoFactorAsync(string code, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/2fa/disable", new TwoFactorCodeRequest(code.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<TwoFactorRecoveryCodesDto> RegenerateRecoveryCodesAsync(string code, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/2fa/recovery-codes", new TwoFactorCodeRequest(code.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Kurtarma kodları oluşturulamadı.");
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

    public async Task<TicketSummaryDto> UpdateTicketDetailsAsync(Guid id, UpdateTicketDetailsRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/tickets/{id}", request, true, ct, TimeSpan.FromSeconds(45));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Araç kaydı güncellenemedi.");
    }

    public async Task DeleteTicketAsync(Guid id, string reason, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Delete, $"api/tickets/{id}", new DeleteTicketRequest(reason.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
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

    public Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<BranchDto>>("api/branches", true, ct);
    public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<AdminUserDto>>("api/admin/users", true, ct);
    public Task<AdminUserDetailDto> GetAdminUserAsync(Guid id, CancellationToken ct = default) => GetAsync<AdminUserDetailDto>($"api/admin/users/{id}", true, ct);

    public async Task<AdminUserDto> CreateAdminUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/admin/users", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Personel hesabı oluşturulamadı.");
    }

    public async Task<AdminUserDetailDto> UpdateAdminUserAsync(Guid id, UpdateAdminUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/admin/users/{id}", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Personel bilgileri güncellenemedi.");
    }

    public async Task<AdminUserDto> SetUserActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Patch, $"api/admin/users/{id}/status", new UpdateUserStatusRequest(active), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Kullanıcı durumu güncellenemedi.");
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/admin/branches", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<BranchDto>(JsonOptions, ct) ?? throw new InvalidOperationException("Şube oluşturulamadı.");
    }

    public Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid? userId = null, int limit = 150, CancellationToken ct = default)
    {
        var path = $"api/audit?limit={Math.Clamp(limit, 1, 250)}";
        if (userId.HasValue) path += "&userId=" + userId.Value;
        return GetAsync<IReadOnlyList<AuditEntryDto>>(path, true, ct);
    }

    public void Logout() => _accessToken = null;

    private async Task<LoginResponse> AcceptLoginAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct) ?? throw new InvalidOperationException("Sunucudan geçerli giriş yanıtı alınamadı.");
        _accessToken = login.AccessToken;
        return login;
    }

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
            if (string.IsNullOrWhiteSpace(_accessToken)) throw new InvalidOperationException("Oturum süreniz sona ermiş. Tekrar giriş yapın.");
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
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("Sunucu yanıt vermedi. Tekrar deneyin."); }
        catch (HttpRequestException ex) { throw new InvalidOperationException($"VALE sunucusuna bağlanılamadı. {GetDeepestMessage(ex)}", ex); }
        catch (Exception ex) when (ex.GetType().FullName?.StartsWith("Java.", StringComparison.Ordinal) == true) { throw new InvalidOperationException($"Android bağlantı hatası: {GetDeepestMessage(ex)}", ex); }
    }

    private static async Task<bool> IsTwoFactorRequiredAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized) return false;
        var body = await response.Content.ReadAsStringAsync(ct);
        return body.Contains("2FA_REQUIRED", StringComparison.OrdinalIgnoreCase) || body.Contains("two_factor_required", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await ReadProblemDetailAsync(response, ct);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => detail ?? "Giriş bilgileri hatalı veya hesabınız henüz onaylanmamış.",
            HttpStatusCode.Forbidden => detail ?? "Bu işlem için yetkiniz yok.",
            HttpStatusCode.Conflict => detail ?? "Bu işlem mevcut kayıtla çakışıyor.",
            HttpStatusCode.TooManyRequests => "Çok fazla deneme yapıldı. Bir süre sonra tekrar deneyin.",
            HttpStatusCode.RequestEntityTooLarge => detail ?? "Gönderilen veri çok büyük.",
            HttpStatusCode.ServiceUnavailable => detail ?? "Sunucu veya bağlı servis şu anda hazır değil.",
            _ => detail ?? $"İşlem tamamlanamadı ({(int)response.StatusCode})."
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
