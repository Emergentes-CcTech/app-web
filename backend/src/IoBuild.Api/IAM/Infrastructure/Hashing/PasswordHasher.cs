namespace IoBuild.Api.IAM.Infrastructure.Hashing;

/// <summary>
/// IAM Infrastructure: hashing. BC-specific infrastructure concern.
/// </summary>
public sealed class PasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
