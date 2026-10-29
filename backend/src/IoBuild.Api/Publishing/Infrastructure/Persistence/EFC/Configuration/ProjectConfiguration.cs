using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration;

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
        });
    }
}
