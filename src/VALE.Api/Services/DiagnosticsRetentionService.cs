using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;

namespace VALE.Api.Services;

public sealed class DiagnosticsRetentionService(IServiceScopeFactory scopes, ILogger<DiagnosticsRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup migrations finish before cleanup begins.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ValeDbContext>();
                var before = DateTimeOffset.UtcNow.AddDays(-30);
                await db.RequestFailures.Where(x => x.OccurredAt < before).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Tanılama saklama süresi uygulanamadı: {Category}", ex.GetType().Name); }
        }
    }
}
