using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;

namespace VALE.Api.Services;

[Index(nameof(OccurredAt))]
[Index(nameof(TraceId))]
[Index(nameof(CompanyId), nameof(OccurredAt))]
public sealed class RequestFailure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(100)] public string TraceId { get; set; } = "";
    [MaxLength(10)] public string Method { get; set; } = "";
    [MaxLength(200)] public string Route { get; set; } = "";
    public int StatusCode { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    [MaxLength(160)] public string Category { get; set; } = "";
}

public sealed class RequestDiagnostics(IServiceScopeFactory scopes, ILogger<RequestDiagnostics> logger)
{
    public async Task RecordAsync(HttpContext context, int statusCode, Exception? exception = null)
    {
        // Do not let automated scans fill the diagnostics table with arbitrary 404 routes.
        if (context.GetEndpoint() is not RouteEndpoint) return;
        if (context.Items.ContainsKey(typeof(RequestFailure))) return;
        context.Items[typeof(RequestFailure)] = true;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ValeDbContext>();
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "Eşleşmeyen adres";
            db.RequestFailures.Add(new RequestFailure
            {
                TraceId = Limit(context.TraceIdentifier, 100),
                Method = Limit(context.Request.Method, 10),
                Route = Limit(route, 200),
                StatusCode = statusCode,
                UserId = Guid.TryParse(context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null,
                CompanyId = Guid.TryParse(context.User.FindFirst("company_id")?.Value, out var companyId) ? companyId : null,
                Category = Limit(exception?.GetType().Name ?? "İstek reddedildi", 160)
            });
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Tanılama kaydı saklanamadı. Kategori: {Category}; iz: {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        }
    }
    private static string Limit(string value, int limit) => value.Length > limit ? value[..limit] : value;
}
