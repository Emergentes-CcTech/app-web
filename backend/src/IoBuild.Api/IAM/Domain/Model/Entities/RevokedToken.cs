namespace IoBuild.Api.IAM.Domain.Model.Entities;

public sealed class RevokedToken
{
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
}
