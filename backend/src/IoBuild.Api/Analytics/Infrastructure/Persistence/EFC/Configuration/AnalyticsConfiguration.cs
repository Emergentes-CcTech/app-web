using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Infrastructure.Persistence.EFC.Configuration;

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
    }
}
