using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Configuration;

public static class PlanConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("plans");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Name).HasMaxLength(100).IsRequired();
            entity.Property(plan => plan.Description).HasMaxLength(500).IsRequired();
            entity.Property(plan => plan.Price).HasPrecision(10, 2).IsRequired();
            entity.Property(plan => plan.Interval).HasMaxLength(50).HasDefaultValue("monthly");
            entity.Property(plan => plan.FeaturesJson).HasColumnType("json");

            entity.HasIndex(plan => plan.Name).IsUnique();
        });
    }
}
