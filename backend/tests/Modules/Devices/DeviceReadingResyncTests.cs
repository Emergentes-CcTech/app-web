using System.Text.Json;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Modules.Tests.Devices;

/// <summary>
/// Un medidor que pierde su contador envía lecturas menores: se rechazan, y se le anuncia la última guardada para que la retome.
/// </summary>
public sealed class DeviceReadingResyncTests
{
    private sealed class RecordingPublisher : IDeviceMqttPublisher
    {
        public List<(string Topic, string Payload)> Published { get; } = [];

        public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default)
        {
            Published.Add((topic, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpSink : IInfluxTelemetrySink
    {
        public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static IoBuildDbContext Db() =>
        new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task A_reading_lower_than_the_stored_one_is_rejected_and_the_meter_learns_where_to_resume()
    {
        await using var db = Db();
        // Id exclusivo: el límite de reanuncios es por dispositivo y dura un minuto
        var deviceId = Random.Shared.Next(100_000, 900_000);
        db.Devices.Add(new Device { Id = deviceId, ProjectId = 10, Name = "WFS-CIS-OUT", Type = "WaterFlowSensor", Subsystem = "MainCistern", LastCumulativeReading = 440.68 });
        await db.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        var registry = new DeviceRegistryService(db, publisher, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Mqtt:Enabled"] = "true" }).Build());
        var telemetry = new DeviceTelemetryService(db, new NoOpSink());

        var rejected = await Assert.ThrowsAsync<NonMonotonicReadingException>(() => telemetry.IngestAsync(
            new TelemetryMessage(deviceId, "evt-1", DateTimeOffset.UtcNow, "online", "{}", 0, CumulativeValue: 412.65)));
        Assert.Equal(deviceId, rejected.DeviceId);

        var now = DateTimeOffset.UtcNow;
        Assert.True(await registry.ResyncAsync(deviceId, now));
        Assert.False(await registry.ResyncAsync(deviceId, now.AddSeconds(5))); // no inunda el broker
        Assert.True(await registry.ResyncAsync(deviceId, now.AddMinutes(2)));

        var (topic, payload) = publisher.Published[0];
        Assert.Equal($"registry/{deviceId}", topic);
        Assert.Equal(440.68, JsonDocument.Parse(payload).RootElement.GetProperty("lastCumulative").GetDouble());
        Assert.Equal(2, publisher.Published.Count);
    }
}
