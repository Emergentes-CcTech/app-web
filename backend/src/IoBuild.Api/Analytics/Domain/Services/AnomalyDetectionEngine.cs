using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Domain.Services;

/// <summary>
/// Motor de reglas determinísticas para la detección inmediata de anomalías operativas en áreas comunes (sin IA).
/// </summary>
public interface IAnomalyDetectionEngine
{
    // Evalúa si existe fuga nocturna continua en cisterna entre las 01:00 AM y 04:00 AM (hora local Lima, UTC-5)
    Task<AnomalyAlert?> EvaluateWaterFlowAsync(Device device, double flowRateM3H, DateTimeOffset occurredAt, CancellationToken ct = default);

    // Evalúa sobrecargas eléctricas en cuartos de bombas hidroneumáticas superiores al 25% de la línea base
    Task<AnomalyAlert?> EvaluatePumpElectricalAsync(Device device, double currentKwh, double baselineKwh, DateTimeOffset occurredAt, CancellationToken ct = default);

    // Evalúa si un concentrador Gateway Hub perdió conectividad en el sótano
    Task<AnomalyAlert?> EvaluateGatewayHeartbeatAsync(Device device, DateTimeOffset currentTime, TimeSpan timeoutThreshold, CancellationToken ct = default);
}

/// <summary>
/// Implementación de reglas determinísticas para detección temprana de fugas y sobrecargas.
/// </summary>
public sealed class AnomalyDetectionEngine(IoBuildDbContext db) : IAnomalyDetectionEngine
{
    // Tarifa referencial de agua potable por metro cúbico en Lima Metropolitana (Sedapal)
    private const decimal SedapalAverageTariffPerM3 = 6.50m;

    public async Task<AnomalyAlert?> EvaluateWaterFlowAsync(Device device, double flowRateM3H, DateTimeOffset occurredAt, CancellationToken ct = default)
    {
        // Regla 1: Fuga Nocturna en Cisterna (01:00 AM a 04:00 AM en hora local de Lima UTC-5)
        var peruTime = occurredAt.ToOffset(TimeSpan.FromHours(-5));
        var hour = peruTime.Hour;
        var isNightWindow = hour >= 1 && hour < 4;
        var threshold = (double)device.NightFlowThresholdM3H;

        if (isNightWindow && flowRateM3H > threshold)
        {
            var excessM3H = flowRateM3H - threshold;
            var estimatedCost = (decimal)(excessM3H * 3.0) * SedapalAverageTariffPerM3;

            var alert = new AnomalyAlert
            {
                ProjectId = device.ProjectId,
                DeviceId = device.Id,
                Subsystem = device.Subsystem,
                AlertType = "NocturnalWaterLeak",
                Severity = "Critical",
                Title = $"Fuga nocturna detectada en {device.Name}",
                Description = $"Caudal medido de {flowRateM3H:F3} m3/h supera el umbral nocturno admisible de {threshold:F3} m3/h entre 01:00 y 04:00 AM.",
                EstimatedOvercostSoles = Math.Round(estimatedCost, 2),
                DetectedAtUtc = occurredAt.UtcDateTime,
                Status = "Active"
            };

            db.AnomalyAlerts.Add(alert);
            await db.SaveChangesAsync(ct);
            return alert;
        }

        return null;
    }

    public async Task<AnomalyAlert?> EvaluatePumpElectricalAsync(Device device, double currentKwh, double baselineKwh, DateTimeOffset occurredAt, CancellationToken ct = default)
    {
        // Regla 2: Sobrecarga en Cuarto de Bombas Hidroneumáticas (>25% sobre línea base)
        if (baselineKwh > 0 && currentKwh > (baselineKwh * 1.25))
        {
            var excessPercentage = ((currentKwh - baselineKwh) / baselineKwh) * 100.0;
            const decimal LuzDelSurAverageTariffPerKwh = 0.85m;
            var excessKwh = currentKwh - baselineKwh;
            var estimatedCost = (decimal)excessKwh * LuzDelSurAverageTariffPerKwh;

            var alert = new AnomalyAlert
            {
                ProjectId = device.ProjectId,
                DeviceId = device.Id,
                Subsystem = device.Subsystem,
                AlertType = "PumpElectricalOverload",
                Severity = "Critical",
                Title = $"Sobrecarga eléctrica en bombas ({device.Name})",
                Description = $"Consumo de {currentKwh:F2} kWh excede la línea base calculada de {baselineKwh:F2} kWh en un {excessPercentage:F1}%.",
                EstimatedOvercostSoles = Math.Round(estimatedCost, 2),
                DetectedAtUtc = occurredAt.UtcDateTime,
                Status = "Active"
            };

            db.AnomalyAlerts.Add(alert);
            await db.SaveChangesAsync(ct);
            return alert;
        }

        return null;
    }

    public async Task<AnomalyAlert?> EvaluateGatewayHeartbeatAsync(Device device, DateTimeOffset currentTime, TimeSpan timeoutThreshold, CancellationToken ct = default)
    {
        // Regla 3: Gateway Hub desconectado en sótano
        var isBasement = (device.LocationArea ?? device.Location ?? string.Empty).Contains("sótano", StringComparison.OrdinalIgnoreCase)
            || (device.LocationArea ?? device.Location ?? string.Empty).Contains("sotano", StringComparison.OrdinalIgnoreCase)
            || device.Subsystem == "MainCistern"
            || device.Subsystem == "PressurePumpsRoom";

        if (device.Type == "GatewayHub" && isBasement)
        {
            var hasNeverReported = !device.LastHeartbeatUtc.HasValue;
            var timeSinceHeartbeat = hasNeverReported ? TimeSpan.MaxValue : currentTime.UtcDateTime - device.LastHeartbeatUtc!.Value;

            if (hasNeverReported || timeSinceHeartbeat > timeoutThreshold)
            {
                var description = hasNeverReported
                    ? "El concentrador en sótano nunca ha emitido señal de latido (heartbeat)."
                    : $"El concentrador en sótano no ha emitido señal de latido durante {timeSinceHeartbeat.TotalMinutes:F0} minutos.";

                var alert = new AnomalyAlert
                {
                    ProjectId = device.ProjectId,
                    DeviceId = device.Id,
                    Subsystem = device.Subsystem,
                    AlertType = "GatewayOfflineInBasement",
                    Severity = "Warning",
                    Title = $"Gateway Hub fuera de línea en sótano ({device.Name})",
                    Description = description,
                    EstimatedOvercostSoles = 0.00m,
                    DetectedAtUtc = currentTime.UtcDateTime,
                    Status = "Active"
                };

                db.AnomalyAlerts.Add(alert);
                await db.SaveChangesAsync(ct);
                return alert;
            }
        }

        return null;
    }
}
