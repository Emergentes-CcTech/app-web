using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Application.Internal;

/// <summary>Resultado de cargar noches de demostración.</summary>
public sealed record DemoNightHistoryResult(int NightsCreated, int ReadingsCreated, IReadOnlyList<BaselineStatus> Baselines);

/// <summary>
/// Solo para demostraciones (Demo:Enabled): carga noches históricas de 01:00 a 04:00 antes de la primera lectura real
/// del medidor de ingreso y de las bombas, para que el detector estadístico tenga línea base. Las diez primeras noches
/// son normales y la última simula agua corriendo (fuga) y bombas activas.
/// Las lecturas acumuladas terminan por debajo de la primera lectura real, así la serie sigue siendo creciente.
/// </summary>
public sealed class DemoNightHistorySeeder(IoBuildDbContext db, StatisticalAnomalyDetector detector)
{
    private const int NormalNights = 10;
    private const int StepMinutes = 5;

    public async Task<DemoNightHistoryResult?> SeedAsync(int projectId, CancellationToken ct = default)
    {
        var excludedWater = await ConsumptionFactsBuilder.NonIntakeWaterSensorIdsAsync(db, projectId, ct);
        var intake = await db.Devices.Where(d => d.ProjectId == projectId && d.Type == "WaterFlowSensor" && !excludedWater.Contains(d.Id))
            .OrderBy(d => d.Id).FirstOrDefaultAsync(ct);
        var pump = await db.Devices.Where(d => d.ProjectId == projectId && d.Type == "CurrentTransformerCT" && d.Subsystem == "PressurePumpsRoom")
            .OrderBy(d => d.Id).FirstOrDefaultAsync(ct);
        if (intake is null && pump is null) return null;

        // Idempotente: si el detector ya tiene noches suficientes (reales o de una carga previa), no se agrega nada
        var current = await detector.EvaluateProjectAsync(projectId, DateTimeOffset.UtcNow, raiseAlerts: false, ct);
        if (current.Any(b => b.NightsInBaseline >= b.NightsRequired))
            return new DemoNightHistoryResult(0, 0, await detector.EvaluateProjectAsync(projectId, DateTimeOffset.UtcNow, raiseAlerts: true, ct));

        // Las noches de demostración terminan el día anterior a la primera lectura real de cualquiera de los dos medidores
        var deviceIds = new[] { intake?.Id, pump?.Id }.Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var firstReal = await db.ConsumptionRecords.Where(c => deviceIds.Contains(c.DeviceId)).MinAsync(c => (DateTime?)c.TimestampUtc, ct) ?? DateTime.UtcNow;
        var lastNight = firstReal.Date.AddDays(-1);
        var firstNight = lastNight.AddDays(-NormalNights);

        var random = new Random(projectId);
        var readings = 0;
        if (intake is not null)
            readings += await AddNightsAsync(intake.Id, projectId, intake.Subsystem, "WaterM3", firstNight, lastNight,
                normal: () => 0.018 + random.NextDouble() * 0.008, last: 0.30, ct);
        if (pump is not null)
            readings += await AddNightsAsync(pump.Id, projectId, pump.Subsystem, "EnergyKwh", firstNight, lastNight,
                normal: () => 0.45 + random.NextDouble() * 0.1, last: 2.0, ct);
        await db.SaveChangesAsync(ct);

        var baselines = await detector.EvaluateProjectAsync(projectId, DateTimeOffset.UtcNow, raiseAlerts: true, ct);
        return new DemoNightHistoryResult(NormalNights + 1, readings, baselines);
    }

    private async Task<int> AddNightsAsync(int deviceId, int projectId, string subsystem, string metric, DateTime firstNight, DateTime lastNight,
        Func<double> normal, double last, CancellationToken ct)
    {
        var rates = new List<(DateTime Night, double PerHour)>();
        for (var night = firstNight; night <= lastNight; night = night.AddDays(1))
            rates.Add((night, night == lastNight ? last : normal()));

        var stepsPerNight = 180 / StepMinutes;
        var total = rates.Sum(r => r.PerHour * 3.0);
        var firstValue = await db.ConsumptionRecords.Where(c => c.DeviceId == deviceId && c.MetricType == metric)
            .OrderBy(c => c.TimestampUtc).Select(c => (double?)c.Value).FirstOrDefaultAsync(ct) ?? total + 100.0;
        var cumulative = Math.Max(0, firstValue - total - 0.01);

        foreach (var (night, perHour) in rates)
        {
            for (var step = 0; step < stepsPerNight; step++)
            {
                cumulative += perHour * StepMinutes / 60.0;
                db.ConsumptionRecords.Add(new ConsumptionRecord
                {
                    ProjectId = projectId,
                    DeviceId = deviceId,
                    Subsystem = subsystem,
                    MetricType = metric,
                    Value = Math.Round(cumulative, 4),
                    TimestampUtc = night.AddHours(6).AddMinutes(step * StepMinutes)
                });
            }
        }
        return rates.Count * stepsPerNight;
    }
}
