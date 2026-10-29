using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration;

public static class UnitConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("units");
            entity.HasKey(unit => unit.Id);
            entity.Property(unit => unit.UnitNumber).HasMaxLength(50).IsRequired();
            entity.Property(unit => unit.Floor).IsRequired();
            entity.Property(unit => unit.RoomNumber).HasMaxLength(20).IsRequired();
            entity.Property(unit => unit.OwnerEmail).HasMaxLength(255);
            entity.Property(unit => unit.OwnerId);
            entity.Property(unit => unit.Status).HasMaxLength(50).HasDefaultValue("available");

            entity.HasIndex(unit => new { unit.ProjectId, unit.Floor, unit.RoomNumber }).IsUnique();
            entity.HasIndex(unit => unit.OwnerEmail);
            entity.HasIndex(unit => unit.OwnerId);
            entity.HasIndex(unit => unit.ProjectId);
        });
    }
}
