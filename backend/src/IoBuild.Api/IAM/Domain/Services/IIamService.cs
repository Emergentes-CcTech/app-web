using IoBuild.Api.IAM.Domain.Model.Commands;

namespace IoBuild.Api.IAM.Domain.Services;

/// <summary>
/// IAM Domain service contract (DDD). IamService implements this.
/// Kept for textbook layering; Program.cs still depends on concrete IamService.
/// </summary>
public interface IIamService
{
    Task RegisterAsync(RegisterUser request, CancellationToken ct = default);
    Task<AuthenticatedUser> SignInAsync(SignIn request, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string token, CancellationToken ct = default);
}
