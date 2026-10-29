using IoBuild.Api.Persistence;

namespace IoBuild.Api.Publishing.Domain.Repositories;

/// <summary>
/// Publishing repository contract (Project).
/// </summary>
public interface IProjectRepository
{
    Task<Project?> FindAsync(int id, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
}
