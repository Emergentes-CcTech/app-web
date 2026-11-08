using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly IoBuildDbContext _dbContext;

    public ProjectRepository(IoBuildDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project?> FindAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.Projects.FindAsync([id], ct);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        await _dbContext.Projects.AddAsync(project, ct);
    }
}
