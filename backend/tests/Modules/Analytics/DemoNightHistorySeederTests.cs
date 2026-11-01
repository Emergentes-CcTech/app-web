using IoBuild.Api.Analytics.Application.Internal;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// Las noches de demostración dan línea base al detector sin romper la serie acumulada de lecturas reales.
/// </summary>
public sealed class DemoNightHistorySeederTests
{
    [Fact]
    public async Task Seeding_creates_ten_normal_nights_and_one_leak_before_the_first_real_reading_once()
    {
        await using var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projects.Add(new Project { Id = 10, Name = "Residencial Los Robles", BuilderId = 1 });
        db.Devices.AddRange(
            new Device { Id = 1, ProjectId = 10, Name = "WFS-CIS-IN", Type = "WaterFlowSensor", Subsystem = "MainCistern", Status = "online" },
            new Device { Id = 3, ProjectId = 10, Name = "CT-BOMB-01", Type = "CurrentTransformerCT", Subsystem = "PressurePumpsRoom", Status = "online" });
        var firstReal = DateTime.UtcNow.Date.AddDays(-1).AddHours(14);
        db.ConsumptionRecords.AddRange(
            new ConsumptionRecord { ProjectId = 10, DeviceId = 1, MetricType = "WaterM3", Subsystem = "MainCistern", Value = 100, TimestampUtc = firstReal },
            new ConsumptionRecord { ProjectId = 10, DeviceId = 3, MetricType = "EnergyKwh", Subsystem = "PressurePumpsRoom", Value = 1000, TimestampUtc = firstReal });
        await db.SaveChangesAsync();
        var detector = new StatisticalAnomalyDetector(db, new ConfigurationBuilder().Build());
        var seeder = new DemoNightHistorySeeder(db, detector);

        var result = await seeder.SeedAsync(10);
        var again = await seeder.SeedAsync(10);

        Assert.NotNull(result);
        Assert.Equal(11, result!.NightsCreated);
        Assert.Equal(0, again!.ReadingsCreated); // idempotente
        Assert.All(result.Baselines, b => Assert.Equal("anomaly", b.Status));
        Assert.All(result.Baselines, b => Assert.Equal(10, b.NightsInBaseline));

        // La serie sigue creciente: toda lectura de demostración queda por debajo de la primera real
        var demoWater = await db.ConsumptionRecords.Where(c => c.DeviceId == 1 && c.TimestampUtc < firstReal).ToListAsync();
        Assert.All(demoWater, r => Assert.True(r.Value < 100));
        Assert.Equal(demoWater.OrderBy(r => r.TimestampUtc).Select(r => r.Value), demoWater.Select(r => r.Value).OrderBy(v => v));

        // Una alerta por medidor para la noche de la fuga, aunque se cargue dos veces
        var alerts = await db.AnomalyAlerts.ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, a => a.AlertType == StatisticalAnomalyDetector.NightFlowAlert);
        Assert.Contains(alerts, a => a.AlertType == StatisticalAnomalyDetector.PumpNightAlert);
    }
}
