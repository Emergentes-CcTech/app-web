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

            // Ignora propiedades de dominio extendidas no presentes como columnas físicas obligatorias
            entity.Ignore(p => p.Address);
            entity.Ignore(p => p.District);
            entity.Ignore(p => p.TotalFloors);
            entity.Ignore(p => p.SedapalSupplyCode);
            entity.Ignore(p => p.LuzDelSurSupplyCode);
            entity.Ignore(p => p.CisternCapacityM3);
            entity.Ignore(p => p.Status);
            entity.Ignore(p => p.AdministratorId);
        });
    }
}
