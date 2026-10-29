using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// Configuración de Entity Framework Core para las entidades y agregados del contexto de Analytics.
/// </summary>
public static class AnalyticsConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceProjection>(entity =>
        {
            entity.ToTable("device_projection");
            entity.HasKey(projection => projection.DeviceId);
            entity.Property(projection => projection.DeviceType).HasMaxLength(64).IsRequired();
            entity.Property(projection => projection.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(projection => projection.OwnerUserId);
            entity.HasIndex(projection => projection.ProjectId);
        });

        modelBuilder.Entity<ProjectProjection>(entity =>
        {
            entity.ToTable("project_projection");
            entity.HasKey(projection => projection.ProjectId);
            entity.Property(projection => projection.Name).HasMaxLength(160).IsRequired();
            entity.Property(projection => projection.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(projection => projection.BuilderUserId);
        });

        modelBuilder.Entity<UnitProjection>(entity =>
        {
            entity.ToTable("unit_projection");
            entity.HasKey(projection => projection.UnitId);
            entity.Property(projection => projection.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(projection => projection.BuilderUserId);
            entity.HasIndex(projection => projection.OwnerUserId);
            entity.HasIndex(projection => projection.ProjectId);
        });

        modelBuilder.Entity<ConsumptionRecord>(entity =>
        {
            entity.ToTable("consumption_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Subsystem).HasMaxLength(64).IsRequired();
            entity.Property(record => record.MetricType).HasMaxLength(64).IsRequired();
            entity.HasIndex(record => new { record.ProjectId, record.TimestampUtc });
            entity.HasIndex(record => new { record.DeviceId, record.TimestampUtc });
        });

        modelBuilder.Entity<AnomalyAlert>(entity =>
        {
            entity.ToTable("anomaly_alerts");
            entity.HasKey(alert => alert.Id);
            entity.Property(alert => alert.Subsystem).HasMaxLength(64).IsRequired();
            entity.Property(alert => alert.AlertType).HasMaxLength(64).IsRequired();
            entity.Property(alert => alert.Severity).HasMaxLength(32).IsRequired();
            entity.Property(alert => alert.Title).HasMaxLength(200).IsRequired();
            entity.Property(alert => alert.Description).HasMaxLength(1000).IsRequired();
            entity.Property(alert => alert.EstimatedOvercostSoles).HasPrecision(10, 2);
            entity.Property(alert => alert.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(alert => new { alert.ProjectId, alert.Status });
            entity.HasIndex(alert => alert.DeviceId);
        });
    }
}
