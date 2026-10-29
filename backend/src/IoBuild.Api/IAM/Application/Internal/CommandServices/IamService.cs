using System.Security.Cryptography;
using System.Text;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Domain.Model.Entities;
using IoBuild.Api.IAM.Domain.Services;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.IAM.Application.Internal.CommandServices;

/// <summary>
/// IAM Application: service orchestrating sign-in / revocation. Stateless
/// over IoBuildDbContext (shared UoW). Keeps original class name for test compatibility.
/// </summary>
public sealed class IamService(
    IoBuildDbContext dbContext,
    PasswordHasher passwordHasher,
    JwtTokenIssuer tokenIssuer,
    IWorkflow<RegisterUser, int> registrationWorkflow) : IIamService
{
    public async Task RegisterAsync(RegisterUser request, CancellationToken cancellationToken = default) =>
        await registrationWorkflow.ExecuteAsync(request, cancellationToken);

    public async Task<AuthenticatedUser> SignInAsync(SignIn request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw new UnauthorizedAccessException("Invalid email or password.");
        var user = await dbContext.IamUsers.SingleOrDefaultAsync(item => item.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash)) throw new UnauthorizedAccessException("Invalid email or password.");
        return new AuthenticatedUser(user.Id, user.Email, user.Role, tokenIssuer.Issue(user));
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(token);
        if (await dbContext.RevokedTokens.AnyAsync(item => item.TokenHash == hash, cancellationToken)) return;
        dbContext.RevokedTokens.Add(new RevokedToken { TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), RevokedAt = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string token, CancellationToken cancellationToken = default) =>
        dbContext.RevokedTokens.AnyAsync(item => item.TokenHash == HashToken(token) && item.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
