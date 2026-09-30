using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Devices;
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
    private const string RefreshTokenStorageKey = "vale_refresh_token_v1";
    public const string ProductionBaseUrl = "https://api.valemyonetim.com/";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private HttpClient _httpClient;
    private string? _accessToken;
    private DateTimeOffset? _sessionExpiresAt;
    public bool IsSessionExpired => IsAuthenticated && _sessionExpiresAt <= DateTimeOffset.UtcNow;
    private readonly SemaphoreSlim _branchContextGate = new(1, 1);
    private IReadOnlyList<BranchDto> _accessibleBranches = Array.Empty<BranchDto>();
    private Guid? _activeBranchId;

    public ApiClient() => _httpClient = CreateHttpClient(EffectiveBaseUrl);
    public bool CustomServerEnabled => Preferences.Default.Get(CustomEnabledPreference, false);
    public string CustomServerUrl => Preferences.Default.Get(CustomUrlPreference, ProductionBaseUrl);
    public string EffectiveBaseUrl => CustomServerEnabled ? NormalizeBaseUrl(CustomServerUrl) : ProductionBaseUrl;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);
    public async Task<bool> IsSmsAvailableAsync()
    {
        var status = await GetAsync<Dictionary<string, bool>>("api/auth/sms/status", false, default);
        return status.GetValueOrDefault("enabled");
    }
    public async Task RequestSmsAsync(string phone, bool verifyPhone = false)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, verifyPhone ? "api/auth/phone/send" : "api/auth/sms/send", new SmsCodeRequest(phone), verifyPhone, default);
        await EnsureSuccessAsync(response, default);
    }
    public async Task<UserDto?> VerifySmsAsync(string phone, string code, string? totp, bool remember, bool verifyPhone = false)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, verifyPhone ? "api/auth/phone/verify" : "api/auth/sms/verify", new SmsVerifyRequest(phone, code, totp, remember, DeviceName), verifyPhone, default);
        if (await IsTwoFactorRequiredAsync(response, default)) throw new TwoFactorRequiredException();
        await EnsureSuccessAsync(response, default);
        return verifyPhone ? null : (await AcceptLoginAsync(response, default)).User;
    }
    public async Task RequestEmailChangeAsync(string email, string password)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/email-change", new ChangeEmailRequest(email, password), true, default);
        await EnsureSuccessAsync(response, default);
    }
    public Guid? ActiveBranchId => _activeBranchId;
    public string? ActiveBranchName => _accessibleBranches.FirstOrDefault(x => x.Id == _activeBranchId)?.Name;
    public IReadOnlyList<BranchDto> AccessibleBranches => _accessibleBranches;

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
            throw new UserFacingException("İnternet bağlantısı yok. Wi‑Fi veya mobil veriyi kontrol edin.");

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
        throw new UserFacingException("VALE sunucusuna şu anda ulaşılamıyor. Birkaç dakika sonra tekrar deneyin.", lastError);
    }

    public async Task TestConnectionAsync(string? overrideUrl = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(overrideUrl) ? EffectiveBaseUrl : NormalizeBaseUrl(overrideUrl);
        using var client = CreateHttpClient(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, "health/ready");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new UserFacingException("Sunucu henüz hazır değil. Tekrar deneyin.");
    }

    public async Task<LoginResponse> LoginAsync(string identifier, string password, bool rememberDevice = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password)) throw new UserFacingException("E-posta/kullanıcı adı ve parola alanlarını doldurun.");
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login", new LoginRequest(identifier.Trim(), password, rememberDevice, DeviceName), false, ct);
        if (await IsTwoFactorRequiredAsync(response, ct)) throw new TwoFactorRequiredException();
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
    }

    public async Task<LoginResponse> LoginWithTwoFactorAsync(string email, string code, bool rememberDevice = false, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/login/2fa", new TwoFactorLoginRequest(email.Trim(), code.Trim(), rememberDevice, DeviceName), false, ct);
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
    }

    public async Task RequestEmailLoginCodeAsync(string email, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/email-code/request", new EmailCodeRequest(email.Trim()), false, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<LoginResponse> VerifyEmailLoginCodeAsync(string email, string code, string? twoFactorCode = null, bool rememberDevice = false, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/email-code/verify", new EmailCodeVerifyRequest(email.Trim(), code.Trim(), string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim(), rememberDevice, DeviceName), false, ct);
        if (await IsTwoFactorRequiredAsync(response, ct)) throw new TwoFactorRequiredException();
        await EnsureSuccessAsync(response, ct);
        return await AcceptLoginAsync(response, ct);
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/register", request, false, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions, ct) ?? throw new UserFacingException("Hesap oluşturma yanıtı alınamadı.");
    }

    public async Task<RegisterResponse> RegisterOwnerAsync(OwnerRegisterRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/registration/owner", request, false, ct, TimeSpan.FromSeconds(60));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions, ct) ?? throw new UserFacingException("Firma hesabı oluşturma yanıtı alınamadı.");
    }

    public async Task<RegisterResponse> RegisterStaffAsync(StaffRegisterRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/registration/staff", request, false, ct, TimeSpan.FromSeconds(60));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions, ct) ?? throw new UserFacingException("Personel başvurusu yanıtı alınamadı.");
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

    public async Task<UserDto> StartDemoAsync(CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/demo", new { }, false, ct);
        await EnsureSuccessAsync(response, ct);
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct)
            ?? throw new UserFacingException("Deneme ekranı açılamadı.");
        TryRemoveRefreshToken();
        _accessToken = session.AccessToken;
        _sessionExpiresAt = session.ExpiresAt;
        ResetBranchContext(session.User.BranchId);
        return session.User;
    }

    public async Task<UserDto> UpdateProfileAsync(string fullName, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/auth/me", new UpdateProfileRequest(fullName.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, ct) ?? throw new UserFacingException("Profil yanıtı alınamadı.");
    }

    public Task<AccountProfileDto> GetAccountProfileAsync(CancellationToken ct = default) => GetAsync<AccountProfileDto>("api/auth/profile", true, ct);

    public Task<EntitlementDto> GetEntitlementAsync(CancellationToken ct = default) =>
        GetAsync<EntitlementDto>("api/billing/entitlement", true, ct);

    public async Task<PurchaseVerificationDto> VerifyGooglePlayPurchaseAsync(string productId, string purchaseToken, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/billing/google-play/verify",
            new VerifyGooglePlayPurchaseRequest(productId, purchaseToken), true, ct, TimeSpan.FromSeconds(45));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PurchaseVerificationDto>(JsonOptions, ct)
            ?? throw new UserFacingException("Satın alma doğrulanamadı. Lütfen yeniden deneyin.");
    }

    public async Task<AccountProfileDto> UpdateAccountProfileAsync(UpdateAccountProfileRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/auth/profile", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AccountProfileDto>(JsonOptions, ct) ?? throw new UserFacingException("Profil kaydedilemedi.");
    }

    public async Task<AccountProfileDto> UpdateProfilePhotoAsync(string contentType, byte[] bytes, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/auth/profile/photo", new ProfilePhotoUploadRequest(contentType, Convert.ToBase64String(bytes)), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AccountProfileDto>(JsonOptions, ct) ?? throw new UserFacingException("Profil fotoğrafı kaydedilemedi.");
    }

    public async Task<AccountProfileDto> DeleteProfilePhotoAsync(CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Delete, "api/auth/profile/photo", new { }, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AccountProfileDto>(JsonOptions, ct) ?? throw new UserFacingException("Profil fotoğrafı kaldırılamadı.");
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
        return await response.Content.ReadFromJsonAsync<TwoFactorSetupDto>(JsonOptions, ct) ?? throw new UserFacingException("2FA kurulumu başlatılamadı.");
    }

    public async Task<TwoFactorRecoveryCodesDto> EnableTwoFactorAsync(string code, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/2fa/enable", new TwoFactorCodeRequest(code.Trim()), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>(JsonOptions, ct) ?? throw new UserFacingException("2FA etkinleştirilemedi.");
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
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>(JsonOptions, ct) ?? throw new UserFacingException("Kurtarma kodları oluşturulamadı.");
    }

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        GetAsync<DashboardDto>(WithActiveBranch("api/dashboard"), true, ct);

    public Task<PagedResponse<TicketSummaryDto>> GetTicketsAsync(string? search = null, bool includeClosed = false, CancellationToken ct = default)
    {
        var path = $"api/tickets?page=1&pageSize=100&includeClosed={includeClosed.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(search)) path += "&search=" + Uri.EscapeDataString(search.Trim());
        path = WithActiveBranch(path);
        return GetAsync<PagedResponse<TicketSummaryDto>>(path, true, ct);
    }

    public Task<TicketDetailDto> GetTicketDetailAsync(Guid id, CancellationToken ct = default) => GetAsync<TicketDetailDto>($"api/tickets/{id}", true, ct);

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var scopedRequest = request.BranchId.HasValue || !_activeBranchId.HasValue
            ? request
            : request with { BranchId = _activeBranchId };
        using var response = await SendJsonAsync(HttpMethod.Post, "api/tickets", scopedRequest, true, ct, TimeSpan.FromSeconds(45));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new UserFacingException("Araç kaydı oluşturulamadı.");
    }

    public async Task<TicketSummaryDto> UpdateTicketDetailsAsync(Guid id, UpdateTicketDetailsRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/tickets/{id}", request, true, ct, TimeSpan.FromSeconds(45));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new UserFacingException("Araç kaydı güncellenemedi.");
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
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct) ?? throw new UserFacingException("Araç durumu güncellenemedi.");
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid id, PaymentMethod method, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, $"api/tickets/{id}/checkout", new CheckoutTicketRequest(method), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions, ct) ?? throw new UserFacingException("Teslim işlemi tamamlanamadı.");
    }

    public Task<ReportSummaryDto> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        GetAsync<ReportSummaryDto>(WithActiveBranch($"api/reports/summary?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}"), true, ct);

    public Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<BranchDto>>("api/branches", true, ct);

    public async Task<IReadOnlyList<BranchDto>> EnsureBranchContextAsync(CancellationToken ct = default)
    {
        if (_accessibleBranches.Count > 0) return _accessibleBranches;

        await _branchContextGate.WaitAsync(ct);
        try
        {
            if (_accessibleBranches.Count > 0) return _accessibleBranches;
            _accessibleBranches = (await GetBranchesAsync(ct)).Where(x => x.IsActive).ToList();
            if (!_activeBranchId.HasValue || _accessibleBranches.All(x => x.Id != _activeBranchId.Value))
                _activeBranchId = _accessibleBranches.FirstOrDefault()?.Id;
            return _accessibleBranches;
        }
        finally
        {
            _branchContextGate.Release();
        }
    }

    public bool SelectActiveBranch(Guid branchId)
    {
        if (_accessibleBranches.All(x => x.Id != branchId)) return false;
        _activeBranchId = branchId;
        return true;
    }
    public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<AdminUserDto>>("api/admin/users", true, ct);
    public Task<AdminUserDetailDto> GetAdminUserAsync(Guid id, CancellationToken ct = default) => GetAsync<AdminUserDetailDto>($"api/admin/users/{id}", true, ct);
    public Task<IReadOnlyList<UserBranchMembershipDto>> GetUserBranchMembershipsAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<UserBranchMembershipDto>>($"api/admin/users/{id}/branches", true, ct);

    public async Task<AdminUserDto> CreateAdminUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/admin/users", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDto>(JsonOptions, ct) ?? throw new UserFacingException("Personel hesabı oluşturulamadı.");
    }

    public async Task<AdminUserDetailDto> UpdateAdminUserAsync(Guid id, UpdateAdminUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/admin/users/{id}", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions, ct) ?? throw new UserFacingException("Personel bilgileri güncellenemedi.");
    }

    public async Task<AdminUserDto> SetUserActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Patch, $"api/admin/users/{id}/status", new UpdateUserStatusRequest(active), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<AdminUserDto>(JsonOptions, ct) ?? throw new UserFacingException("Kullanıcı durumu güncellenemedi.");
    }

    public async Task<IReadOnlyList<UserBranchMembershipDto>> UpdateUserBranchMembershipsAsync(
        Guid id,
        UpdateUserBranchMembershipsRequest request,
        CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/admin/users/{id}/branches", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserBranchMembershipDto>>(JsonOptions, ct)
            ?? throw new UserFacingException("Şube erişimleri güncellenemedi.");
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/admin/branches", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<BranchDto>(JsonOptions, ct) ?? throw new UserFacingException("Şube oluşturulamadı.");
    }

    public Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid? userId = null, int limit = 150, CancellationToken ct = default)
    {
        var path = $"api/audit?limit={Math.Clamp(limit, 1, 250)}";
        if (userId.HasValue) path += "&userId=" + userId.Value;
        return GetAsync<IReadOnlyList<AuditEntryDto>>(path, true, ct);
    }

    public Task<IReadOnlyList<RegistrationRequestDto>> GetPendingRegistrationsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<RegistrationRequestDto>>("api/registration/pending", true, ct);

    public async Task<RegistrationRequestDto> DecideRegistrationAsync(Guid id, bool approve, string? note = null, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, $"api/registration/{id}/decision", new RegistrationDecisionRequest(approve, note), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RegistrationRequestDto>(JsonOptions, ct) ?? throw new UserFacingException("Başvuru sonucu alınamadı.");
    }

    public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<NotificationDto>>($"api/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}&limit=150", true, ct);

    public async Task<NotificationDto> SetNotificationReadAsync(Guid id, bool read = true, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Patch, $"api/notifications/{id}", new MarkNotificationReadRequest(read), true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<NotificationDto>(JsonOptions, ct) ?? throw new UserFacingException("Bildirim güncellenemedi.");
    }

    public async Task ReadAllNotificationsAsync(CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/notifications/read-all", new { }, true, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<PushStatusDto> GetPushStatusAsync(CancellationToken ct = default) =>
        GetAsync<PushStatusDto>("api/push/status", true, ct);

    public async Task<PushRegistrationDto> RegisterPushTokenAsync(PushRegistrationRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, "api/push/register", request, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PushRegistrationDto>(JsonOptions, ct) ?? throw new UserFacingException("Bildirim cihaz kaydı oluşturulamadı.");
    }

    public async Task UnregisterPushTokenAsync(PushRegistrationRequest request, CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/push/unregister", request, true, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<PushTestResponse> SendPushTestAsync(CancellationToken ct = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/push/test", new { }, true, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PushTestResponse>(JsonOptions, ct) ?? throw new UserFacingException("Push test yanıtı alınamadı.");
    }

    public async Task<LoginResponse?> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        var refreshToken = await TryReadRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        try
        {
            using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/refresh", new RefreshSessionRequest(refreshToken, DeviceName), false, ct);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                TryRemoveRefreshToken();
                return null;
            }
            await EnsureSuccessAsync(response, ct);
            return await AcceptLoginAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            // Network/cold-start errors must not destroy a still-valid remembered session.
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A malformed/transient server response must not crash application startup or
            // destroy a token that can be retried after the service is healthy again.
            return null;
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await PushTokenManager.DetachAsync(ct);
        var refreshToken = await TryReadRefreshTokenAsync();

        try
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                using var response = await SendJsonAsync(HttpMethod.Post, "api/auth/logout", new RefreshSessionRequest(refreshToken, DeviceName), false, ct, TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // Local logout always succeeds; an unreachable server session expires naturally.
        }
        finally
        {
            Logout();
        }
    }

    public void Logout()
    {
        _accessToken = null;
        TryRemoveRefreshToken();
        ResetBranchContext();
    }

    private async Task<LoginResponse> AcceptLoginAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct) ?? throw new UserFacingException("Sunucudan geçerli giriş yanıtı alınamadı.");
        _accessToken = login.AccessToken;
        _sessionExpiresAt = login.ExpiresAt;
        if (string.IsNullOrWhiteSpace(login.RefreshToken))
            TryRemoveRefreshToken();
        else
            await TryWriteRefreshTokenAsync(login.RefreshToken);
        ResetBranchContext(login.User.BranchId);
        return login;
    }

    private static async Task<string?> TryReadRefreshTokenAsync()
    {
        try { return await SecureStorage.Default.GetAsync(RefreshTokenStorageKey); }
        catch
        {
            TryRemoveRefreshToken();
            return null;
        }
    }

    private static async Task TryWriteRefreshTokenAsync(string token)
    {
        try { await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, token); }
        catch
        {
            // Bozulmuş veya kullanılamayan Android keystore girişi normal oturumu engellememeli.
            TryRemoveRefreshToken();
        }
    }

    private static void TryRemoveRefreshToken()
    {
        try { SecureStorage.Default.Remove(RefreshTokenStorageKey); } catch { }
    }

    private static string DeviceName =>
        $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim();

    private async Task<T> GetAsync<T>(string path, bool authorized, CancellationToken ct)
    {
        using var response = await SendWithTimeoutAsync(() => CreateRequest(HttpMethod.Get, path, authorized), TimeSpan.FromSeconds(25), ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct) ?? throw new UserFacingException("Sunucudan geçerli veri alınamadı.");
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
            if (string.IsNullOrWhiteSpace(_accessToken)) throw new UserFacingException("Oturum süreniz sona ermiş. Tekrar giriş yapın.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(Func<HttpRequestMessage> factory, TimeSpan timeout, CancellationToken ct)
    {
        using var request = factory();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        try
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization is not null)
                ExpireSession();
            return response;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new UserFacingException("Sunucu yanıt vermedi. Tekrar deneyin."); }
        catch (HttpRequestException ex) { throw new UserFacingException("Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.", ex); }
        catch (Exception ex) when (ex.GetType().FullName?.StartsWith("Java.", StringComparison.Ordinal) == true) { throw new UserFacingException("Telefonunuz sunucuya bağlanamadı. Bağlantınızı kontrol edip tekrar deneyin.", ex); }
    }

    public void ExpireSession()
    {
        if (!IsAuthenticated) return;
        Logout();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            App.ShowLogin();
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
                await page.DisplayAlertAsync("Oturum sona erdi", "Güvenliğiniz için oturumunuz kapatıldı. Yeniden giriş yapabilirsiniz.", "Tamam");
        });
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
            _ => detail ?? "İşlem tamamlanamadı. Bilgilerinizi kontrol edip yeniden deneyin."
        };
        throw new UserFacingException(message);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Array)
                    .SelectMany(x => x.Value.EnumerateArray()).Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()).Distinct().Take(4).ToArray();
                if (messages.Length > 0) return FriendlyValidationMessage(messages);
            }
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) return detail.GetString();
            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) return message.GetString();
            if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String) return title.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    private static string FriendlyValidationMessage(IReadOnlyList<string?> messages)
    {
        var combined = string.Join(" ", messages.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (combined.Contains("required", StringComparison.OrdinalIgnoreCase))
            return "Zorunlu alanları doldurup tekrar deneyin.";
        if (combined.Contains("valid", StringComparison.OrdinalIgnoreCase) || combined.Contains("format", StringComparison.OrdinalIgnoreCase))
            return "Bilgilerden biri beklenen biçimde değil. Alanları kontrol edip tekrar deneyin.";
        if (combined.Contains("maximum length", StringComparison.OrdinalIgnoreCase) || combined.Contains("must be a string", StringComparison.OrdinalIgnoreCase))
            return "Yazdığınız bilgilerden biri izin verilen uzunluğu aşıyor.";
        return "Girilen bilgiler doğrulanamadı. Alanları kontrol edip tekrar deneyin.";
    }

    private void RebuildClient(string baseUrl)
    {
        var old = _httpClient;
        _httpClient = CreateHttpClient(baseUrl);
        _accessToken = null;
        // Refresh tokens are issued by one API origin. Never forward a production token
        // to a user-selected custom server (or the reverse) after an endpoint change.
        TryRemoveRefreshToken();
        ResetBranchContext();
        old.Dispose();
    }

    private string WithActiveBranch(string path)
    {
        if (!_activeBranchId.HasValue) return path;
        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}branchId={_activeBranchId.Value:D}";
    }

    private void ResetBranchContext(Guid? preferredBranchId = null)
    {
        _accessibleBranches = Array.Empty<BranchDto>();
        _activeBranchId = preferredBranchId;
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
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new UserFacingException("Geçerli bir HTTPS sunucu adresi girin.");
        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            throw new UserFacingException("HTTP yalnızca localhost geliştirme sunucusunda kullanılabilir. Uzak sunucular HTTPS olmalıdır.");
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

    public void Dispose()
    {
        _httpClient.Dispose();
        _branchContextGate.Dispose();
    }
}
