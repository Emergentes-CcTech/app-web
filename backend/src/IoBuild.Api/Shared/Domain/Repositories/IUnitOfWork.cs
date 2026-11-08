namespace IoBuild.Api.Shared.Domain.Repositories;

/// <summary>
/// Shared kernel: Unit of Work abstraction.
/// IoBuildDbContext is the single UoW for the monolith (single DB, single
/// migration history). Per-BC repositories share this UoW to keep the
/// current transactional consistency while making the DDD layering explicit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
