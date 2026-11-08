namespace IoBuild.Api.IAM.Domain.Model.Aggregates;

/// <summary>
/// IAM Domain aggregate. Physically lives under IAM/Domain/Model/Aggregates/
/// but keeps namespace IoBuild.Api.Persistence for backward compatibility
/// (tests import IoBuild.Api.Persistence). In DDD terms, IamUser is the sole
/// IAM aggregate root; RevokedToken is a supporting entity.
/// Single IoBuildDbContext remains the UoW; configuration lives in
/// IAM/Infrastructure/Persistence/EFC/Configuration/IamConfiguration.cs.
/// </summary>
public sealed class IamUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
