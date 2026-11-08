using IoBuild.Api.Persistence;

namespace IoBuild.Api.Subscriptions.Domain.Repositories;

public interface IPlanRepository
{
    Task<Plan?> FindByIdAsync(int id, CancellationToken ct = default);
    Task<Plan?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<IEnumerable<Plan>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Plan plan, CancellationToken ct = default);
    Task UpdateAsync(Plan plan, CancellationToken ct = default);
}
