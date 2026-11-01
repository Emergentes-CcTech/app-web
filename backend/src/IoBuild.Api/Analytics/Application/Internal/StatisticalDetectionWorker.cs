using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Application.Internal;

/// <summary>
/// Evalúa cada 30 minutos la última noche cerrada de cada condominio con el detector estadístico.
/// Las alertas no se duplican: hay una por medidor y por noche.
/// </summary>
public sealed class StatisticalDetectionWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<StatisticalDetectionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Anomaly:StatisticalDetectionEnabled", true)) return;
        // Deja que terminen las migraciones del arranque antes de consultar
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
                var detector = scope.ServiceProvider.GetRequiredService<StatisticalAnomalyDetector>();
                var projectIds = await db.Projects.Select(p => p.Id).ToListAsync(stoppingToken);
                foreach (var projectId in projectIds)
                    await detector.EvaluateProjectAsync(projectId, DateTimeOffset.UtcNow, raiseAlerts: true, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "La detección estadística falló en este ciclo; se reintenta en {Minutes} minutos.", Interval.TotalMinutes);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
