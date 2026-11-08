using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Domain.Services;

/// <summary>Estado de la línea base de un medidor y resultado de la última noche evaluada.</summary>
public sealed record BaselineStatus(
    int DeviceId,
    string DeviceName,
    string Metric,
    string Unit,
    int NightsInBaseline,
    int NightsRequired,
    double? Median,
    double? Threshold,
    string? LastNight,
    double? LastNightValue,
    string Status); // learning | normal | anomaly | no-data

/// <summary>
/// Detección estadística de anomalías nocturnas. Cada edificio tiene su propia línea base: la mediana de sus últimas
/// noches y su dispersión robusta (MAD). Una noche es anómala si supera mediana + 3·σ (con pisos mínimos).
/// Dos señales, en la ventana de 01:00 a 04:00 hora de Lima, cuando casi nadie usa agua:
///   - Flujo mínimo nocturno del medidor de ingreso de la cisterna (m³/h): un mínimo alto es agua que corre sin uso.
///   - Energía nocturna de las bombas (kWh): si trabajan de madrugada, la cisterna se está vaciando.
/// Complementa las reglas de umbral fijo; la IA generativa solo explica estas alertas, no las decide.
/// </summary>
public sealed class StatisticalAnomalyDetector(IoBuildDbContext db, IConfiguration configuration)
{
    public const string NightFlowMetric = "NightMinimumFlow";
    public const string PumpNightMetric = "PumpNightEnergy";
    public const string NightFlowAlert = "StatisticalNightFlow";
    public const string PumpNightAlert = "PumpNightRuntime";

    // Ventana 01:00–04:00 en Lima (UTC-5, sin horario de verano) = 06:00–09:00 UTC
    private const int WindowStartUtcHour = 6;
    private const int WindowEndUtcHour = 9;
    private const int MinReadingsPerHour = 6;
    private const int MaxBaselineNights = 14;
    private const double SigmaFactor = 3.0;
    private const double RelativeFloor = 0.5;   // al menos 50% sobre lo normal
    private const decimal WaterTariffPerM3 = 6.50m;
    private const decimal EnergyTariffPerKwh = 0.85m;

    private int NightsRequired => Math.Max(3, configuration.GetValue("Anomaly:MinBaselineNights", 5));

    public async Task<IReadOnlyList<BaselineStatus>> EvaluateProjectAsync(int projectId, DateTimeOffset now, bool raiseAlerts, CancellationToken ct = default)
    {
        var excludedWater = await ConsumptionFactsBuilder.NonIntakeWaterSensorIdsAsync(db, projectId, ct);
        var devices = await db.Devices
            .Where(d => d.ProjectId == projectId && d.Status != "Unlinked"
                && (d.Type == "WaterFlowSensor" || (d.Type == "CurrentTransformerCT" && d.Subsystem == "PressurePumpsRoom")))
            .ToListAsync(ct);

        var results = new List<BaselineStatus>();
        foreach (var device in devices.Where(d => !excludedWater.Contains(d.Id)).OrderBy(d => d.Id))
        {
            var isWater = device.Type == "WaterFlowSensor";
            var nights = await NightlyValuesAsync(device.Id, isWater ? "WaterM3" : "EnergyKwh", isWater, now, ct);
            var status = Evaluate(device.Id, device.Name, isWater, nights);
            results.Add(status);
            if (raiseAlerts && status.Status == "anomaly") await RaiseAlertAsync(device.ProjectId, device.Id, device.Name, device.Subsystem, isWater, status, ct);
        }
        if (raiseAlerts) await db.SaveChangesAsync(ct);
        return results;
    }

    // Valor de cada noche completa: mínimo horario de agua (m³/h) o energía total de la ventana (kWh)
    private async Task<List<(DateTime Night, double Value)>> NightlyValuesAsync(int deviceId, string metric, bool isWater, DateTimeOffset now, CancellationToken ct)
    {
        var from = now.UtcDateTime.Date.AddDays(-(MaxBaselineNights + 2));
        var readings = await db.ConsumptionRecords
            .Where(c => c.DeviceId == deviceId && c.MetricType == metric && c.TimestampUtc >= from
                && c.TimestampUtc.Hour >= WindowStartUtcHour && c.TimestampUtc.Hour < WindowEndUtcHour)
            .Select(c => new { c.TimestampUtc, c.Value })
            .ToListAsync(ct);

        var lastClosedNight = now.UtcDateTime.Hour >= WindowEndUtcHour ? now.UtcDateTime.Date : now.UtcDateTime.Date.AddDays(-1);
        return readings
            .GroupBy(r => r.TimestampUtc.Date)
            .Where(g => g.Key <= lastClosedNight)
            .Select(g =>
            {
                // Lecturas acumuladas: el ritmo de cada hora es lo consumido entre su primera y última lectura
                // dividido por el tiempo que realmente cubren (no por una hora entera, que lo subestimaría)
                var hours = g.GroupBy(r => r.TimestampUtc.Hour)
                    .Where(h => h.Count() >= MinReadingsPerHour)
                    .Select(h => (Span: (h.Max(r => r.TimestampUtc) - h.Min(r => r.TimestampUtc)).TotalHours, Delta: h.Max(r => r.Value) - h.Min(r => r.Value)))
                    .Where(h => h.Span >= 0.25)
                    .Select(h => h.Delta / h.Span)
                    .ToList();
                return (Night: g.Key, Hours: hours);
            })
            // Una noche cuenta si tiene al menos 2 de las 3 horas con lecturas suficientes
            .Where(n => n.Hours.Count >= 2)
            // Agua: el menor ritmo horario (m³/h). Bombas: ritmo medio por las 3 horas de la ventana (kWh)
            .Select(n => (n.Night, Value: Math.Round(isWater ? n.Hours.Min() : n.Hours.Average() * 3.0, 4)))
            .OrderBy(n => n.Night)
            .ToList();
    }

    private BaselineStatus Evaluate(int deviceId, string deviceName, bool isWater, List<(DateTime Night, double Value)> nights)
    {
        var metric = isWater ? NightFlowMetric : PumpNightMetric;
        var unit = isWater ? "m3/h" : "kWh";
        if (nights.Count == 0) return new BaselineStatus(deviceId, deviceName, metric, unit, 0, NightsRequired, null, null, null, null, "no-data");

        var last = nights[^1];
        var baseline = nights.Take(nights.Count - 1).TakeLast(MaxBaselineNights).Select(n => n.Value).ToList();
        var lastNight = last.Night.ToString("yyyy-MM-dd");
        if (baseline.Count < NightsRequired)
            return new BaselineStatus(deviceId, deviceName, metric, unit, baseline.Count, NightsRequired, null, null, lastNight, last.Value, "learning");

        var median = Median(baseline);
        var sigma = 1.4826 * Median(baseline.Select(v => Math.Abs(v - median)).ToList());
        var absoluteFloor = isWater ? 0.05 : 0.2;
        var threshold = Math.Round(median + Math.Max(SigmaFactor * sigma, Math.Max(RelativeFloor * median, absoluteFloor)), 4);
        var status = last.Value > threshold ? "anomaly" : "normal";
        return new BaselineStatus(deviceId, deviceName, metric, unit, baseline.Count, NightsRequired, Math.Round(median, 4), threshold, lastNight, last.Value, status);
    }

    private async Task RaiseAlertAsync(int projectId, int deviceId, string deviceName, string subsystem, bool isWater, BaselineStatus s, CancellationToken ct)
    {
        var type = isWater ? NightFlowAlert : PumpNightAlert;
        var night = DateTime.SpecifyKind(DateTime.Parse(s.LastNight!), DateTimeKind.Utc);
        var detectedAt = night.AddHours(WindowEndUtcHour);
        // Una alerta por medidor y por noche, aunque el detector corra varias veces
        if (await db.AnomalyAlerts.AnyAsync(a => a.DeviceId == deviceId && a.AlertType == type && a.DetectedAtUtc == detectedAt, ct)) return;

        var excess = (decimal)(s.LastNightValue!.Value - s.Median!.Value);
        db.AnomalyAlerts.Add(new AnomalyAlert
        {
            ProjectId = projectId,
            DeviceId = deviceId,
            Subsystem = subsystem,
            AlertType = type,
            Severity = s.LastNightValue > s.Threshold * 2 ? "Critical" : "Warning",
            Title = isWater ? $"Flujo nocturno fuera de lo normal en {deviceName}" : $"Bombas trabajando de madrugada más de lo normal ({deviceName})",
            Description = isWater
                ? $"El flujo mínimo nocturno fue {s.LastNightValue:F3} m3/h; lo normal en este edificio es {s.Median:F3} m3/h (umbral {s.Threshold:F3}) según {s.NightsInBaseline} noches. Agua que corre sin uso: posible fuga."
                : $"Las bombas consumieron {s.LastNightValue:F2} kWh entre 01:00 y 04:00; lo normal en este edificio es {s.Median:F2} kWh (umbral {s.Threshold:F2}) según {s.NightsInBaseline} noches. La cisterna podría estar vaciándose por una fuga.",
            // Agua: el exceso corre todo el día; bombas: solo la energía extra de la ventana nocturna
            EstimatedOvercostSoles = Math.Round(isWater ? excess * 24 * WaterTariffPerM3 : excess * EnergyTariffPerKwh, 2),
            DetectedAtUtc = detectedAt,
            Status = "Active"
        });
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
