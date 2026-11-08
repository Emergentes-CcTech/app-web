using IoBuild.Api.Persistence;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IProjectQueryService
{
    Task<IEnumerable<Project>> GetProjectsByBuilderAsync(int builderId, CancellationToken cancellationToken = default);
    Task<Project?> GetProjectByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllProjectsAsync(CancellationToken cancellationToken = default);
}
