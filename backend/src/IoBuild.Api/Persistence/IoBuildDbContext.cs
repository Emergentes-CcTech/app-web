using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

/// <summary>
/// Shared kernel DbContext — single DB, single migration history (5 migrations).
/// Per-BC EF configurations live under each BC's
/// Infrastructure/Persistence/EFC/Configuration/* and are applied here
/// to keep the DDD layering explicit while preserving the monolith's
/// transactional consistency. No split DB, no new migrations.
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
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Plan> Plans => Set<Plan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Shared kernel
        global::IoBuild.Api.Shared.Infrastructure.Persistence.EFC.Configuration.SharedConfiguration.ConfigureFoundation(modelBuilder);

        // Per-BC configurations (delegated for readability, behavior identical)
        global::IoBuild.Api.IAM.Infrastructure.Persistence.EFC.Configuration.IamConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.ProjectConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.UnitConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration.ClientConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Profiles.Infrastructure.Persistence.EFC.Configuration.ProfileConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration.SubscriptionConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration.PlanConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Devices.Infrastructure.Persistence.EFC.Configuration.DeviceConfiguration.Configure(modelBuilder);
        global::IoBuild.Api.Analytics.Infrastructure.Persistence.EFC.Configuration.AnalyticsConfiguration.Configure(modelBuilder);
    }
}

// Shared kernel entities (remain here; detailed per-BC aggregates live under their BC/Domain folders)
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
