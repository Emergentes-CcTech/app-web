using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Configuration;

public static class ClientConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(client => client.Id);
            entity.Property(client => client.FullName).HasMaxLength(200).IsRequired();
            entity.Property(client => client.ProjectName).HasMaxLength(200).IsRequired();
            entity.Property(client => client.AccountStatement).HasMaxLength(50).IsRequired();
            entity.Property(client => client.BuilderId).IsRequired();
            entity.Property(client => client.ProjectId).IsRequired();
            entity.Property(client => client.Email).HasMaxLength(150);
            entity.Property(client => client.PhoneNumber).HasMaxLength(50);
            entity.Property(client => client.Address).HasMaxLength(255);
            entity.Property(client => client.UnitId);
            entity.Property(client => client.UnitNumber).HasMaxLength(50);

            entity.HasIndex(client => client.BuilderId);
            entity.HasIndex(client => client.ProjectId);
            entity.HasIndex(client => client.Email);
            entity.HasIndex(client => client.UnitId);
        });
    }
}
