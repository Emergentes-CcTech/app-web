using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Analytics.Infrastructure.Adapters;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// Pruebas unitarias para el motor determinístico de detección de anomalías y el adaptador cognitivo AI simulado.
/// </summary>
public sealed class AnomalyEngineAndAiTests
{
    private static IoBuildDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IoBuildDbContext(options);
    }

    private sealed class NoOpSink : IInfluxTelemetrySink
    {
        public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task EvaluateWaterFlow_DetectsNocturnalLeak_Between1And4Am()
    {
        // Dispositivo caudalímetro en cisterna con umbral de 0.15 m3/h
        await using var db = CreateInMemoryDb();
        var engine = new AnomalyDetectionEngine(db);

        var device = new Device
        {
            Id = 1,
            ProjectId = 101,
            Name = "WFS-CIS-01",
            Type = "WaterFlowSensor",
            Subsystem = "MainCistern",
            NightFlowThresholdM3H = 0.15m
        };

        // Lectura de 0.28 m3/h a las 02:30 AM hora de Lima (UTC-5)
        var leakTimestamp = new DateTimeOffset(2026, 10, 15, 2, 30, 0, TimeSpan.FromHours(-5));
        var alert = await engine.EvaluateWaterFlowAsync(device, 0.28, leakTimestamp);

        Assert.NotNull(alert);
        Assert.Equal("NocturnalWaterLeak", alert.AlertType);
        Assert.Equal("Critical", alert.Severity);
        Assert.True(alert.EstimatedOvercostSoles > 0m);

        // Verifica persistencia en base de datos
        var persisted = await db.AnomalyAlerts.FirstOrDefaultAsync(a => a.DeviceId == 1);
        Assert.NotNull(persisted);
        Assert.Equal("Active", persisted.Status);
    }

    [Fact]
    public async Task EvaluateWaterFlow_IgnoresFlow_OutsideNightWindow()
    {
        await using var db = CreateInMemoryDb();
        var engine = new AnomalyDetectionEngine(db);

        var device = new Device
        {
            Id = 2,
            ProjectId = 101,
            Name = "WFS-CIS-01",
            Type = "WaterFlowSensor",
            Subsystem = "MainCistern",
            NightFlowThresholdM3H = 0.15m
        };

        // Lectura de 0.35 m3/h a las 11:30 AM hora de Lima (UTC-5)
        var dayTimestamp = new DateTimeOffset(2026, 10, 15, 11, 30, 0, TimeSpan.FromHours(-5));
        var alert = await engine.EvaluateWaterFlowAsync(device, 0.35, dayTimestamp);

        Assert.Null(alert);
        Assert.Empty(await db.AnomalyAlerts.ToListAsync());
    }

    [Fact]
    public async Task EvaluatePumpElectrical_DetectsOverload_WhenExceeding25PercentBaseline()
    {
        await using var db = CreateInMemoryDb();
        var engine = new AnomalyDetectionEngine(db);

        var device = new Device
        {
            Id = 3,
            ProjectId = 101,
            Name = "CT-BOMB-01",
            Type = "CurrentTransformerCT",
            Subsystem = "PressurePumpsRoom"
        };

        // Línea base de 4.0 kWh, consumo registrado de 5.5 kWh (+37.5%)
        var occurredAt = DateTimeOffset.UtcNow;
        var alert = await engine.EvaluatePumpElectricalAsync(device, currentKwh: 5.5, baselineKwh: 4.0, occurredAt);

        Assert.NotNull(alert);
        Assert.Equal("PumpElectricalOverload", alert.AlertType);
        Assert.Equal("Critical", alert.Severity);
        Assert.True(alert.EstimatedOvercostSoles > 0m);
    }

    [Fact]
    public async Task EvaluateGatewayHeartbeat_DetectsOffline_WhenTimeoutExceededInBasement()
    {
        await using var db = CreateInMemoryDb();
        var engine = new AnomalyDetectionEngine(db);

        var device = new Device
        {
            Id = 4,
            ProjectId = 101,
            Name = "GW-SOT-01",
            Type = "GatewayHub",
            LocationArea = "Sótano 1 - Cisterna",
            Subsystem = "MainCistern",
            LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        var alert = await engine.EvaluateGatewayHeartbeatAsync(device, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));

        Assert.NotNull(alert);
        Assert.Equal("GatewayOfflineInBasement", alert.AlertType);
        Assert.Equal("Warning", alert.Severity);
    }

    [Fact]
    public async Task DeviceTelemetryService_PersistsConsumptionRecord_AndInvokesAnomalyEngine()
    {
        await using var db = CreateInMemoryDb();
        var engine = new AnomalyDetectionEngine(db);
        var sink = new NoOpSink();
        var service = new DeviceTelemetryService(db, sink, engine);

        var device = new Device
        {
            Id = 5,
            ProjectId = 200,
            Name = "WFS-CIS-01",
            Type = "WaterFlowSensor",
            Subsystem = "MainCistern",
            NightFlowThresholdM3H = 0.15m
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        // Lectura nocturna a las 02:15 AM hora de Lima con caudal alto
        var timestamp = new DateTimeOffset(2026, 10, 20, 2, 15, 0, TimeSpan.FromHours(-5));
        var msg = new TelemetryMessage(
            DeviceId: 5,
            EventId: "evt-night-leak",
            OccurredAt: timestamp,
            Status: "Online",
            ReportedJson: "{\"flow_m3h\":0.32,\"cumulative_m3\":10.5}",
            EnergyKwh: 0,
            CumulativeValue: 10.5);

        var success = await service.IngestAsync(msg);
        Assert.True(success);

        // Verifica que se haya guardado el registro en ConsumptionRecords
        var record = await db.ConsumptionRecords.FirstOrDefaultAsync(r => r.DeviceId == 5);
        Assert.NotNull(record);
        Assert.Equal(10.5, record.Value);
        Assert.Equal("WaterM3", record.MetricType);

        // Verifica que se haya disparado y guardado la alerta de fuga nocturna
        var alert = await db.AnomalyAlerts.FirstOrDefaultAsync(a => a.DeviceId == 5);
        Assert.NotNull(alert);
        Assert.Equal("NocturnalWaterLeak", alert.AlertType);
    }

    [Fact]
    public async Task MockAiCognitiveAdapter_ExplainsConsumptionAndAliquotImpact()
    {
        await using var db = CreateInMemoryDb();
        db.Projects.Add(new Project { Id = 10, Name = "Residencial Los Robles" });
        db.Units.Add(new Unit(10, "402", null, 4, "402") { Id = 402, AliquotPercentage = 3.8500m });
        db.Devices.Add(new Device
        {
            Id = 1,
            ProjectId = 10,
            Name = "WFS-CIS-01",
            Type = "WaterFlowSensor",
            NightFlowThresholdM3H = 0.1500m
        });
        db.ConsumptionRecords.Add(new ConsumptionRecord
        {
            ProjectId = 10,
            DeviceId = 1,
            Subsystem = "MainCistern",
            MetricType = "WaterM3",
            Value = 42.5,
            TimestampUtc = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc)
        });
        db.AnomalyAlerts.Add(new AnomalyAlert
        {
            ProjectId = 10,
            DeviceId = 1,
            Subsystem = "MainCistern",
            AlertType = "NocturnalWaterLeak",
            EstimatedOvercostSoles = 150.00m,
            Title = "Fuga en cisterna",
            Description = "Fuga nocturna detectada",
            DetectedAtUtc = new DateTime(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var adapter = new MockAiCognitiveAdapter(db);
        var explanation = await adapter.ExplainConsumptionVariationAsync(10, 402, "2026-10", "¿Por qué varió el consumo?");

        Assert.NotNull(explanation);
        Assert.True(explanation.HasAnomalies);
        Assert.Contains("Residencial Los Robles", explanation.Summary);
        Assert.Contains("Departamento 402", explanation.Summary);
        Assert.Contains("3.8500%", explanation.AliquotImpact);
        Assert.Contains("0.15 m3/h", explanation.WaterAnalysis);
        Assert.NotEmpty(explanation.Recommendations);
    }
}
