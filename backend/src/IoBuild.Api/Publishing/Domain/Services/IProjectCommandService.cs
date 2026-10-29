using IoBuild.Api.Persistence;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IProjectCommandService
{
    Task<Project> CreateProjectAsync(string name, string description, string location, int totalUnits, int builderId, string? imageUrl, CancellationToken cancellationToken = default);
    Task DefineProjectStructureAsync(int projectId, int floors, int unitsPerFloor, List<int>? floorNumbers, CancellationToken cancellationToken = default);
}
