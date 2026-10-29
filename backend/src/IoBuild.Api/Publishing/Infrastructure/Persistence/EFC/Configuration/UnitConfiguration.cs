using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// Configuración de Entity Framework Core para la entidad Unit (Departamento) en Catastro.
/// </summary>
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
            entity.Property(unit => unit.ResidentFullName).HasMaxLength(255);
            entity.Property(unit => unit.AccountStatus).HasMaxLength(50).HasDefaultValue("Al día");
            entity.Property(unit => unit.AreaM2).HasPrecision(10, 2).HasDefaultValue(75.0m);
            entity.Property(unit => unit.AliquotPercentage).HasPrecision(7, 4).HasDefaultValue(0.0000m);

            // Propiedades calculadas o alias que mapean a columnas físicas
            entity.Ignore(u => u.FloorNumber);
            entity.Ignore(u => u.ResidentEmail);

            entity.HasIndex(unit => new { unit.ProjectId, unit.Floor, unit.RoomNumber }).IsUnique();
            entity.HasIndex(unit => unit.OwnerEmail);
            entity.HasIndex(unit => unit.OwnerId);
            entity.HasIndex(unit => unit.ProjectId);
        });
    }
}
