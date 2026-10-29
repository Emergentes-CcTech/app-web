namespace IoBuild.Api.Shared.Domain.Repositories;

/// <summary>
/// Shared kernel: base repository contract for aggregates.
/// Concrete repositories per BC (IAM, Publishing, etc.) extend this.
/// Currently IoBuildDbContext acts as the shared Unit of Work / repository
/// implementation; this interface documents the DDD intent without
/// introducing a breaking abstraction layer over EF Core.
/// </summary>
public interface IBaseRepository<TEntity> where TEntity : class
{
    Task<TEntity?> FindAsync(int id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Remove(TEntity entity);
}
