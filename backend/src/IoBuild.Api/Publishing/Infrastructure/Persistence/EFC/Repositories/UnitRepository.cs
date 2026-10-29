using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;

public class UnitRepository : IUnitRepository
{
    private readonly IoBuildDbContext _dbContext;

    public UnitRepository(IoBuildDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit?> FindByIdAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.Units.FindAsync([id], ct);
    }

    public async Task<IEnumerable<Unit>> FindByProjectIdAsync(int projectId, CancellationToken ct = default)
    {
        return await _dbContext.Units
            .Where(u => u.ProjectId == projectId)
            .OrderBy(u => u.Floor)
            .ThenBy(u => u.RoomNumber)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Unit>> FindByOwnerIdAsync(int ownerId, CancellationToken ct = default)
    {
        return await _dbContext.Units
            .Where(u => u.OwnerId == ownerId)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Unit>> FindByOwnerEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbContext.Units
            .Where(u => u.OwnerEmail == email)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Unit>> ListAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Units.OrderBy(u => u.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Unit unit, CancellationToken ct = default)
    {
        await _dbContext.Units.AddAsync(unit, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Unit> units, CancellationToken ct = default)
    {
        await _dbContext.Units.AddRangeAsync(units, ct);
    }

    public Task UpdateAsync(Unit unit, CancellationToken ct = default)
    {
        _dbContext.Units.Update(unit);
        return Task.CompletedTask;
    }
}
