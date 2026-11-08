using IoBuild.Api.Persistence;

namespace IoBuild.Api.Publishing.Domain.Repositories;

public interface IUnitRepository
{
    Task<Unit?> FindByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Unit>> FindByProjectIdAsync(int projectId, CancellationToken ct = default);
    Task<IEnumerable<Unit>> FindByOwnerIdAsync(int ownerId, CancellationToken ct = default);
    Task<IEnumerable<Unit>> FindByOwnerEmailAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<Unit>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Unit unit, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Unit> units, CancellationToken ct = default);
    Task UpdateAsync(Unit unit, CancellationToken ct = default);
}
