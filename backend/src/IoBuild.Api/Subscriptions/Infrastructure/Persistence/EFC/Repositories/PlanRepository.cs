using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly IoBuildDbContext _dbContext;

    public PlanRepository(IoBuildDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Plan?> FindByIdAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.Plans.FindAsync([id], ct);
    }

    public async Task<Plan?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        return await _dbContext.Plans.FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<IEnumerable<Plan>> ListAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Plans.OrderBy(p => p.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Plan plan, CancellationToken ct = default)
    {
        await _dbContext.Plans.AddAsync(plan, ct);
    }

    public Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        _dbContext.Plans.Update(plan);
        return Task.CompletedTask;
    }
}
