using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// Configuración de Entity Framework Core para la entidad Project (Condominio) en Catastro.
/// </summary>
public static class ProjectConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(200).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(2000).IsRequired();
            entity.Property(project => project.Location).HasMaxLength(500).IsRequired();
            entity.Property(project => project.ImageUrl).HasMaxLength(2000);

            // Datos de catastro persistidos (columnas agregadas por EfMigrationRunner en bases existentes)
            entity.Property(p => p.Address).HasMaxLength(255).IsRequired();
            entity.Property(p => p.District).HasMaxLength(100).IsRequired();
            entity.Property(p => p.TotalFloors).IsRequired();
            entity.Property(p => p.SedapalSupplyCode).HasMaxLength(32).IsRequired();
            entity.Property(p => p.LuzDelSurSupplyCode).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Status).HasMaxLength(32).IsRequired();

            // AdministratorId se persiste a través de BuilderId (mismo valor); la capacidad de cisterna aún no se registra
            entity.Ignore(p => p.CisternCapacityM3);
            entity.Ignore(p => p.AdministratorId);
        });
    }
}
