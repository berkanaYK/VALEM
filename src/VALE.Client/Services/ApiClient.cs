using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VALE.Contracts;

namespace VALE.Client.Services;

public sealed class ValeApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ApiClient(ClientSettings settings, SessionService session)
{
    private readonly HttpClient _http = new() { BaseAddress = settings.GetApiBaseUri(), Timeout = TimeSpan.FromSeconds(30) };
    private readonly JsonSerializerOptions _json = CreateJsonOptions();

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", request, false, cancellationToken);

    public Task<DashboardDto> GetDashboardAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = branchId.HasValue ? $"?branchId={branchId}" : string.Empty;
        return SendAsync<DashboardDto>(HttpMethod.Get, $"api/dashboard{query}", null, true, cancellationToken);
    }

    public Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<BranchDto>>(HttpMethod.Get, "api/branches", null, true, cancellationToken);

    public Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<BranchDto>(HttpMethod.Post, "api/admin/branches", request, true, cancellationToken);

    public Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminUserDto>>(HttpMethod.Get, "api/admin/users", null, true, cancellationToken);

    public Task<AdminUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, "api/admin/users", request, true, cancellationToken);

    public Task<PagedResponse<TicketSummaryDto>> GetTicketsAsync(
        Guid? branchId,
        string? search,
        bool includeClosed,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"includeClosed={includeClosed.ToString().ToLowerInvariant()}", "pageSize=100" };
        if (branchId.HasValue)
        {
            parameters.Add($"branchId={branchId}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        return SendAsync<PagedResponse<TicketSummaryDto>>(
            HttpMethod.Get,
            "api/tickets?" + string.Join("&", parameters),
            null,
            true,
            cancellationToken);
    }

    public Task<TicketSummaryDto> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<TicketSummaryDto>(HttpMethod.Post, "api/tickets", request, true, cancellationToken);

    public Task<TicketSummaryDto> UpdateTicketStatusAsync(
        Guid ticketId,
        TicketStatus status,
        CancellationToken cancellationToken = default) =>
        SendAsync<TicketSummaryDto>(
            HttpMethod.Patch,
            $"api/tickets/{ticketId}/status",
            new UpdateTicketStatusRequest(status),
            true,
            cancellationToken);

    public Task<CheckoutResponse> CheckoutAsync(
        Guid ticketId,
        PaymentMethod method,
        CancellationToken cancellationToken = default) =>
        SendAsync<CheckoutResponse>(
            HttpMethod.Post,
            $"api/tickets/{ticketId}/checkout",
            new CheckoutTicketRequest(method),
            true,
            cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        object? body,
        bool authorize,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: _json);
        }

        if (authorize)
        {
            if (!session.IsAuthenticated)
            {
                throw new ValeApiException("Oturum süresi dolmuş. Lütfen yeniden giriş yapın.", 401);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ValeApiException(
                $"VALE sunucusuna ulaşılamadı. İnternet bağlantısını ve API adresini kontrol edin. ({exception.Message})",
                0);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ValeApiException(
                    await ReadProblemAsync(response, cancellationToken),
                    (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<T>(_json, cancellationToken);
            return result ?? throw new ValeApiException("Sunucudan boş yanıt alındı.", (int)response.StatusCode);
        }
    }

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"İşlem başarısız oldu (HTTP {(int)response.StatusCode}).";
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("detail", out var detail) && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }

            if (document.RootElement.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Sunucu ProblemDetails dışında bir yanıt verdiyse güvenli genel mesajı kullan.
        }

        return fallback;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
