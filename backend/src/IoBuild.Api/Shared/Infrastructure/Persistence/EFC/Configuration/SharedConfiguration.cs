using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Shared.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// Shared kernel persistence configuration.
/// IoBuildDbContext remains the single DbContext (see Persistence/IoBuildDbContext.cs)
/// with a single migration history (5 migrations). Per-BC configurations live in
/// their own BC/Infrastructure/Persistence/EFC/Configuration/* files and are
/// applied from OnModelCreating. This file documents the shared tables.
/// </summary>
public static class SharedConfiguration
{
    public static void ConfigureFoundation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FoundationRecord>(entity =>
        {
            entity.ToTable("foundation_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<IntegrationDispatch>(entity =>
        {
            entity.ToTable("integration_dispatch");
            entity.HasKey(dispatch => dispatch.Id);
            entity.HasIndex(dispatch => dispatch.IdempotencyKey).IsUnique();
            entity.HasIndex(dispatch => new { dispatch.Status, dispatch.NextAttemptAt });
            entity.HasIndex(dispatch => new { dispatch.OrderingKey, dispatch.Sequence }).IsUnique();
            entity.Property(dispatch => dispatch.OwnerModule).HasMaxLength(80).IsRequired();
            entity.Property(dispatch => dispatch.Channel).HasMaxLength(80).IsRequired();
            entity.Property(dispatch => dispatch.OrderingKey).HasMaxLength(200).IsRequired();
            entity.Property(dispatch => dispatch.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(dispatch => dispatch.Payload).IsRequired();
            entity.Property(dispatch => dispatch.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
