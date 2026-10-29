using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Infrastructure.Persistence.EFC.Configuration;

public static class DeviceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity => { entity.ToTable("devices"); entity.HasKey(device => device.Id); entity.HasIndex(device => device.MacAddress).IsUnique(); entity.HasIndex(device => new { device.ProjectId, device.UnitId, device.Type }).IsUnique(); entity.Property(device => device.MacAddress).HasMaxLength(17); entity.Property(device => device.Source).HasMaxLength(30); entity.Property(device => device.Name).HasMaxLength(200).IsRequired(); entity.Property(device => device.Type).HasMaxLength(100).IsRequired(); entity.Property(device => device.Location).HasMaxLength(500).IsRequired(); entity.Property(device => device.Status).HasMaxLength(40).IsRequired(); });
        modelBuilder.Entity<DeviceShadow>(entity => { entity.ToTable("device_shadows"); entity.HasKey(shadow => shadow.DeviceId); entity.Property(shadow => shadow.DesiredJson).IsRequired(); });
        modelBuilder.Entity<DeviceCommand>(entity => { entity.ToTable("device_commands"); entity.HasKey(command => command.Id); entity.HasIndex(command => command.CommandId).IsUnique(); entity.HasIndex(command => command.DeviceId); entity.HasIndex(command => new { command.DeviceId, command.AcknowledgedAt, command.IssuedAt }); entity.Property(command => command.CommandId).HasMaxLength(64); entity.Property(command => command.DesiredJson).IsRequired(); });
        modelBuilder.Entity<DeviceTelemetry>(entity => { entity.ToTable("device_telemetry"); entity.HasKey(telemetry => telemetry.Id); entity.HasIndex(telemetry => telemetry.EventId).IsUnique(); entity.HasIndex(telemetry => new { telemetry.DeviceId, telemetry.OccurredAt }); entity.Property(telemetry => telemetry.EventId).HasMaxLength(128); entity.Property(telemetry => telemetry.Status).HasMaxLength(40); entity.Property(telemetry => telemetry.ReportedJson).IsRequired(); });
        modelBuilder.Entity<TelemetryRecovery>(entity => { entity.ToTable("telemetry_recovery"); entity.HasKey(recovery => recovery.Id); entity.HasIndex(recovery => recovery.EventId).IsUnique(); entity.Property(recovery => recovery.EventId).HasMaxLength(128); });
        modelBuilder.Entity<UnitOwnerProjection>(entity => { entity.ToTable("unit_owner_projections"); entity.HasKey(item => item.UnitId); entity.HasIndex(item => new { item.UnitId, item.OwnerUserId }).IsUnique(); });
        modelBuilder.Entity<DeviceRegistryTombstone>(entity => { entity.ToTable("device_registry_tombstones"); entity.HasKey(item => item.DeviceId); });
    }
}
