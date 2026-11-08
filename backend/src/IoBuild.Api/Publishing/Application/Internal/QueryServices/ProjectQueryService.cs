using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Application.Internal.QueryServices;

public class ProjectQueryService : IProjectQueryService
{
    private readonly IoBuildDbContext _dbContext;

    public ProjectQueryService(IoBuildDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Project>> GetProjectsByBuilderAsync(int builderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .Where(p => p.BuilderId == builderId)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetProjectByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects.OrderBy(p => p.Id).ToListAsync(cancellationToken);
    }
}
