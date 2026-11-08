using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.IAM.Infrastructure.Persistence.EFC.Configuration;

/// <summary>
/// IAM EF configuration. Applied from IoBuildDbContext.OnModelCreating.
/// </summary>
public static class IamConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IamUser>(entity =>
        {
            entity.ToTable("iam_users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(80).IsRequired();
        });
        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("iam_revoked_tokens");
            entity.HasKey(token => token.TokenHash);
            entity.Property(token => token.TokenHash).HasMaxLength(64);
        });
    }
}
