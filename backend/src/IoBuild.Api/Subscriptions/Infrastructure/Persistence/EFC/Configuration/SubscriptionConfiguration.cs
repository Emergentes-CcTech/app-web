using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration;

public static class SubscriptionConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(subscription => subscription.Id);
            entity.HasIndex(subscription => new { subscription.BuilderId, subscription.PlanId, subscription.Status });
            entity.Property(subscription => subscription.Status).HasMaxLength(40).IsRequired();
        });
        modelBuilder.Entity<SubscriptionWebhook>(entity =>
        {
            entity.ToTable("subscription_webhooks");
            entity.HasKey(webhook => webhook.EventId);
            entity.Property(webhook => webhook.EventId).HasMaxLength(255);
            entity.Property(webhook => webhook.EventType).HasMaxLength(120).IsRequired();
        });
    }
}
