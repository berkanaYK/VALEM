using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ApiClient : IDisposable
{
    private const string ApiUrlPreference = "vale_api_base_url";
    public const string ProductionBaseUrl = "https://vale-api-5fvb.onrender.com/";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private HttpClient _httpClient;
    private string? _accessToken;

    public ApiClient()
    {
        _httpClient = CreateHttpClient(SavedBaseUrl);
    }

    public string SavedBaseUrl => Preferences.Default.Get(ApiUrlPreference, ProductionBaseUrl);

    public void UseProductionServer()
    {
        Configure(ProductionBaseUrl);
    }

    public void Configure(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        Preferences.Default.Set(ApiUrlPreference, normalized);

        if (_httpClient.BaseAddress?.AbsoluteUri.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        var previous = _httpClient;
        _httpClient = CreateHttpClient(normalized);
        _accessToken = null;
        previous.Dispose();
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("E-posta ve parola alanlarını doldurun.");
        }

        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Post, "api/auth/login", new LoginRequest(email.Trim(), password)),
            ct);

        await EnsureSuccessAsync(response, ct);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Sunucudan geçerli giriş yanıtı alınamadı.");

        _accessToken = login.AccessToken;
        ApplyToken();
        return login;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/dashboard"),
            ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<DashboardDto>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Dashboard verisi alınamadı.");
    }

    public async Task<PagedResponse<TicketSummaryDto>> GetTicketsAsync(string? search = null, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        var url = "api/tickets?page=1&pageSize=100&includeClosed=false";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += "&search=" + Uri.EscapeDataString(search.Trim());
        }

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<PagedResponse<TicketSummaryDto>>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Araç listesi alınamadı.");
    }

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Post, "api/tickets", request),
            ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Araç kaydı oluşturulamadı.");
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid id, TicketStatus status, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Patch, $"api/tickets/{id}/status", new UpdateTicketStatusRequest(status)),
            ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TicketSummaryDto>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Araç durumu güncellenemedi.");
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid id, PaymentMethod method, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Post, $"api/tickets/{id}/checkout", new CheckoutTicketRequest(method)),
            ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<CheckoutResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Teslim işlemi tamamlanamadı.");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var request = requestFactory();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (attempt == 0 && IsTransient(response.StatusCode))
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    continue;
                }

                return response;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                lastError = ex;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
            }

            if (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        throw new InvalidOperationException(
            "VALE sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin. Sunucu uyku modundaysa ilk bağlantı biraz daha uzun sürebilir.",
            lastError);
    }

    private HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string path, T value)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(value, options: JsonOptions)
        };
        return request;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private void ApplyToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(_accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new InvalidOperationException("Oturum açılmamış. Tekrar giriş yapın.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadProblemDetailAsync(response, ct);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "E-posta adresi veya parola hatalı.",
            HttpStatusCode.Forbidden => "Bu işlem için yetkiniz yok.",
            HttpStatusCode.TooManyRequests => "Çok fazla deneme yapıldı. Kısa bir süre sonra tekrar deneyin.",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => "VALE sunucusu şu anda hazırlanıyor. Birkaç saniye sonra tekrar deneyin.",
            _ => string.IsNullOrWhiteSpace(detail)
                ? $"Sunucu hatası: {(int)response.StatusCode} {response.ReasonPhrase}"
                : detail
        };

        throw new InvalidOperationException(message);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }
            if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return body.Length <= 300 ? body : body[..300];
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(NormalizeBaseUrl(baseUrl)),
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("Geçerli bir http/https API adresi girin.");
        }

        var builder = new UriBuilder(uri);
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        return builder.Uri.AbsoluteUri;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public void Dispose() => _httpClient.Dispose();
}
