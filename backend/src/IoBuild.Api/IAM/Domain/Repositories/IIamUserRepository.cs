using IoBuild.Api.IAM.Domain.Model.Aggregates;

namespace IoBuild.Api.IAM.Domain.Repositories;

/// <summary>
/// IAM Domain repository contract. IoBuildDbContext is the shared implementation
/// (single DB, no split). This interface documents the BC boundary for future courses.
/// </summary>
public interface IIamUserRepository
{
    Task<IamUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(IamUser user, CancellationToken ct = default);
}
