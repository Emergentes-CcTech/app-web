using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

/// <summary>
/// DbContext del núcleo compartido para el monolito modular IoBuild.
/// Unifica la persistencia de los 6 Bounded Contexts preservando fronteras transaccionales.
/// </summary>
public sealed class IoBuildDbContext(DbContextOptions<IoBuildDbContext> options) : DbContext(options)
{
    public DbSet<FoundationRecord> FoundationRecords => Set<FoundationRecord>();
    public DbSet<IamUser> IamUsers => Set<IamUser>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<IntegrationDispatch> IntegrationDispatches => Set<IntegrationDispatch>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionWebhook> SubscriptionWebhooks => Set<SubscriptionWebhook>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceShadow> DeviceShadows => Set<DeviceShadow>();
    public DbSet<DeviceCommand> DeviceCommands => Set<DeviceCommand>();
    public DbSet<DeviceTelemetry> DeviceTelemetry => Set<DeviceTelemetry>();
    public DbSet<TelemetryRecovery> TelemetryRecoveries => Set<TelemetryRecovery>();
    public DbSet<UnitOwnerProjection> UnitOwnerProjections => Set<UnitOwnerProjection>();
    public DbSet<DeviceRegistryTombstone> DeviceRegistryTombstones => Set<DeviceRegistryTombstone>();
    public DbSet<DeviceProjection> DeviceProjections => Set<DeviceProjection>();
    public DbSet<ProjectProjection> ProjectProjections => Set<ProjectProjection>();
    public DbSet<UnitProjection> UnitProjections => Set<UnitProjection>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<CommonArea> CommonAreas => Set<CommonArea>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<ConsumptionRecord> ConsumptionRecords => Set<ConsumptionRecord>();
    public DbSet<AnomalyAlert> AnomalyAlerts => Set<AnomalyAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Núcleo compartido
        global::IoBuild.Api.Shared.Infrastructure.Persistence.EFC.Configuration.SharedConfiguration.ConfigureFoundation(modelBuilder);

        // Configuraciones delegadas por Bounded Context
        global::IoBuild.Api.IAM.Infrastructure.Persistence.EFC.Configuration.IamConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.ProjectConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.UnitConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.ClientConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Profiles.Infrastructure.Persistence.EFC.Configuration.ProfileConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration.SubscriptionConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration.PlanConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Devices.Infrastructure.Persistence.EFC.Configuration.DeviceConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Analytics.Infrastructure.Persistence.EFC.Configuration.AnalyticsConfiguration.Configure(modelBuilder);

        // Mapeo de la entidad de áreas comunes de condominios
        modelBuilder.Entity<CommonArea>(entity =>
        {
            entity.ToTable("common_areas");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(150).IsRequired();
            entity.Property(c => c.Location).HasMaxLength(255);
            entity.Property(c => c.Description).HasMaxLength(500);
        });
    }
}

// Entidades del núcleo compartido
public sealed class FoundationRecord { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
public enum DispatchStatus { Pending, InProgress, DeadLetter, Completed }
public sealed class IntegrationDispatch
{
    public long Id { get; set; }
    public string OwnerModule { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string OrderingKey { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DispatchStatus Status { get; set; } = DispatchStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
