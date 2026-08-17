using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ApiClient : IDisposable
{
    private const string ApiUrlPreference = "vale_api_base_url";
    private const string ProductionBaseUrl = "https://vale-api-5fvb.onrender.com/";
    private HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _accessToken;

    public string SavedBaseUrl => Preferences.Default.Get(ApiUrlPreference, ProductionBaseUrl);

    public void Configure(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Geçerli bir http/https API adresi girin.");

        var normalized = uri.ToString().EndsWith('/') ? uri.ToString() : uri + "/";
        Preferences.Default.Set(ApiUrlPreference, normalized);
        _httpClient.BaseAddress = new Uri(normalized);
        ApplyToken();
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        EnsureConfigured();
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(email.Trim(), password), ct);
        await EnsureSuccessAsync(response, ct);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Giriş yanıtı alınamadı.");
        _accessToken = login.AccessToken;
        ApplyToken();
        return login;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureAuthenticated();
        return await _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard", ct)
            ?? throw new InvalidOperationException("Dashboard verisi alınamadı.");
    }

    public async Task<PagedResponse<TicketSummaryDto>> GetTicketsAsync(string? search = null, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        var url = "api/tickets?page=1&pageSize=100&includeClosed=false";
        if (!string.IsNullOrWhiteSpace(search))
            url += "&search=" + Uri.EscapeDataString(search.Trim());
        return await _httpClient.GetFromJsonAsync<PagedResponse<TicketSummaryDto>>(url, ct)
            ?? throw new InvalidOperationException("Araç listesi alınamadı.");
    }

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await _httpClient.PostAsJsonAsync("api/tickets", request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Araç kaydı oluşturulamadı.");
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid id, TicketStatus status, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/tickets/{id}/status")
        {
            Content = JsonContent.Create(new UpdateTicketStatusRequest(status))
        };
        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Durum güncellenemedi.");
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid id, PaymentMethod method, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await _httpClient.PostAsJsonAsync($"api/tickets/{id}/checkout", new CheckoutTicketRequest(method), ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<CheckoutResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Teslim işlemi tamamlanamadı.");
    }

    private void ApplyToken() => _httpClient.DefaultRequestHeaders.Authorization =
        string.IsNullOrWhiteSpace(_accessToken) ? null : new AuthenticationHeaderValue("Bearer", _accessToken);

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress is null) throw new InvalidOperationException("Önce API adresini girin.");
    }

    private void EnsureAuthenticated()
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(_accessToken)) throw new InvalidOperationException("Oturum açılmamış.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Sunucu hatası: {(int)response.StatusCode} {body}");
    }

    public void Dispose() => _httpClient.Dispose();
}
