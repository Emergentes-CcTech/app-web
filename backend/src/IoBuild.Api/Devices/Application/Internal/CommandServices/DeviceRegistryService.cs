using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Devices.Infrastructure.Mqtt;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Application.Internal.CommandServices;

public sealed class DeviceRegistryService(IoBuildDbContext db, IDeviceMqttPublisher mqtt, Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    private static readonly TimeSpan ResyncInterval = TimeSpan.FromMinutes(1);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, DateTimeOffset> LastResync = new();

    // lastCumulative permite a un medidor reiniciado retomar su contador sin retroceder
    public Task AnnounceAsync(Device device, CancellationToken cancellationToken = default) =>
        PublishWhenEnabledAsync($"registry/{device.Id}", JsonSerializer.Serialize(new { deviceId = device.Id, type = device.Type, lastCumulative = device.LastCumulativeReading }), cancellationToken);

    /// <summary>
    /// Reanuncia un dispositivo cuya lectura se rechazó por retroceder. Como máximo una vez por minuto,
    /// para que un medidor con un fallo persistente no inunde el broker.
    /// </summary>
    public async Task<bool> ResyncAsync(int deviceId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (LastResync.TryGetValue(deviceId, out var last) && now - last < ResyncInterval) return false;
        var device = await db.Devices.FindAsync([deviceId], cancellationToken);
        if (device is null) return false;
        LastResync[deviceId] = now;
        await AnnounceAsync(device, cancellationToken);
        return true;
    }

    public void QueueTombstone(int deviceId)
    {
        if (!db.DeviceRegistryTombstones.Local.Any(item => item.DeviceId == deviceId)) db.DeviceRegistryTombstones.Add(new DeviceRegistryTombstone { DeviceId = deviceId });
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        foreach (var device in await db.Devices.OrderBy(device => device.Id).ToListAsync(cancellationToken)) await AnnounceAsync(device, cancellationToken);
        var tombstones = await db.DeviceRegistryTombstones.Where(item => item.PublishedAt == null).OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        foreach (var tombstone in tombstones)
        {
            await PublishWhenEnabledAsync($"registry/{tombstone.DeviceId}", string.Empty, cancellationToken);
            tombstone.PublishedAt = DateTimeOffset.UtcNow;
            tombstone.PublishAttempts++;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task AnnounceAllAsync(CancellationToken cancellationToken = default) => ReconcileAsync(cancellationToken);

    private Task PublishWhenEnabledAsync(string topic, string payload, CancellationToken cancellationToken) =>
        configuration.GetValue<bool>("Mqtt:Enabled")
            ? mqtt.PublishAsync(topic, payload, qos1: true, retain: true, cancellationToken)
            : Task.CompletedTask;
}

public sealed class DeviceRegistryAnnouncer(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DeviceRegistryService>().ReconcileAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<DeviceCommandService>().RepublishPendingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeviceRegistryAnnouncer] Deferred reconciliation: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
