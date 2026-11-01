using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// La detección estadística aprende la línea base nocturna de cada edificio y marca las noches que se salen de ella.
/// </summary>
public sealed class StatisticalAnomalyDetectorTests
{
    // Mediodía del 20 de octubre: la última noche cerrada es la del 20 (01:00–04:00 Lima = 06:00–09:00 UTC)
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 17, 0, 0, TimeSpan.Zero);

    private static IoBuildDbContext Db()
    {
        var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projects.Add(new Project { Id = 10, Name = "Residencial Los Robles", TotalUnits = 25 });
        db.Devices.AddRange(
            new Device { Id = 1, ProjectId = 10, Name = "WFS-CIS-IN", Type = "WaterFlowSensor", Subsystem = "MainCistern", Status = "online" },
            new Device { Id = 2, ProjectId = 10, Name = "WFS-CIS-OUT", Type = "WaterFlowSensor", Subsystem = "MainCistern", Status = "online" },
            new Device { Id = 3, ProjectId = 10, Name = "CT-BOMB-01", Type = "CurrentTransformerCT", Subsystem = "PressurePumpsRoom", Status = "online" });
        return db;
    }

    // Genera lecturas acumuladas cada 5 minutos en la ventana nocturna, con el ritmo indicado (unidades por hora)
    private static void Night(IoBuildDbContext db, int deviceId, string metric, DateTime night, double perHour, ref double cumulative)
    {
        for (var minute = 0; minute < 180; minute += 5)
        {
            cumulative += perHour / 12.0;
            db.ConsumptionRecords.Add(new ConsumptionRecord
            {
                ProjectId = 10, DeviceId = deviceId, MetricType = metric, Subsystem = "MainCistern",
                Value = cumulative, TimestampUtc = night.AddHours(6).AddMinutes(minute)
            });
        }
    }

    private static StatisticalAnomalyDetector Detector(IoBuildDbContext db, int minNights = 5) =>
        new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Anomaly:MinBaselineNights"] = minNights.ToString() }).Build());

    private static async Task SeedNightsAsync(IoBuildDbContext db, double lastNightWater, double lastNightPump)
    {
        double water = 100, pump = 1000;
        // Diez noches normales: ~0.02 m³/h de flujo mínimo y ~0.5 kWh de bombas por hora
        for (var day = 10; day <= 19; day++)
        {
            var night = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc);
            Night(db, 1, "WaterM3", night, 0.02 + (day % 3) * 0.002, ref water);
            Night(db, 3, "EnergyKwh", night, 0.5 + (day % 2) * 0.05, ref pump);
        }
        var last = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        Night(db, 1, "WaterM3", last, lastNightWater, ref water);
        Night(db, 3, "EnergyKwh", last, lastNightPump, ref pump);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_normal_night_stays_within_the_building_baseline()
    {
        await using var db = Db();
        await SeedNightsAsync(db, lastNightWater: 0.022, lastNightPump: 0.52);

        var statuses = await Detector(db).EvaluateProjectAsync(10, Now, raiseAlerts: true);

        Assert.Equal(2, statuses.Count); // ingreso de la cisterna y bombas; la impulsión no se evalúa
        Assert.All(statuses, s => Assert.Equal("normal", s.Status));
        var water = statuses.Single(s => s.Metric == StatisticalAnomalyDetector.NightFlowMetric);
        Assert.Equal(10, water.NightsInBaseline);
        Assert.InRange(water.Median!.Value, 0.019, 0.025);
        Assert.Empty(db.AnomalyAlerts);
    }

    [Fact]
    public async Task A_night_with_water_running_raises_one_statistical_alert_with_its_overcost()
    {
        await using var db = Db();
        await SeedNightsAsync(db, lastNightWater: 0.30, lastNightPump: 2.0);
        var detector = Detector(db);

        var statuses = await detector.EvaluateProjectAsync(10, Now, raiseAlerts: true);
        await detector.EvaluateProjectAsync(10, Now, raiseAlerts: true); // el worker puede correr varias veces

        Assert.All(statuses, s => Assert.Equal("anomaly", s.Status));
        var alerts = await db.AnomalyAlerts.OrderBy(a => a.AlertType).ToListAsync();
        Assert.Equal(2, alerts.Count);
        var leak = alerts.Single(a => a.AlertType == StatisticalAnomalyDetector.NightFlowAlert);
        Assert.Equal("Critical", leak.Severity);
        Assert.Contains("lo normal en este edificio", leak.Description);
        Assert.InRange(leak.EstimatedOvercostSoles, 40m, 46m); // (0.30 - 0.022) m³/h × 24 h × S/ 6.50
        Assert.Contains(alerts, a => a.AlertType == StatisticalAnomalyDetector.PumpNightAlert);
    }

    [Fact]
    public async Task With_few_nights_the_detector_keeps_learning_and_raises_nothing()
    {
        await using var db = Db();
        double water = 100;
        for (var day = 18; day <= 20; day++)
            Night(db, 1, "WaterM3", new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc), day == 20 ? 0.5 : 0.02, ref water);
        await db.SaveChangesAsync();

        var statuses = await Detector(db).EvaluateProjectAsync(10, Now, raiseAlerts: true);

        var waterStatus = statuses.Single(s => s.Metric == StatisticalAnomalyDetector.NightFlowMetric);
        Assert.Equal("learning", waterStatus.Status);
        Assert.Equal(2, waterStatus.NightsInBaseline);
        Assert.Equal(5, waterStatus.NightsRequired);
        Assert.Equal("no-data", statuses.Single(s => s.Metric == StatisticalAnomalyDetector.PumpNightMetric).Status);
        Assert.Empty(db.AnomalyAlerts);
    }
}
