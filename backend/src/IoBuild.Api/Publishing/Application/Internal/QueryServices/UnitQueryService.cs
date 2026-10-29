using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Queries;

namespace IoBuild.Api.Publishing.Application.Internal.QueryServices;

public class UnitQueryService : IUnitQueryService
{
    private readonly IUnitRepository _unitRepository;

    public UnitQueryService(IUnitRepository unitRepository)
    {
        _unitRepository = unitRepository;
    }

    public async Task<IEnumerable<Unit>> Handle(GetAllUnitsQuery query, CancellationToken ct = default)
    {
        return await _unitRepository.ListAllAsync(ct);
    }

    public async Task<Unit?> Handle(GetUnitByIdQuery query, CancellationToken ct = default)
    {
        return await _unitRepository.FindByIdAsync(query.Id, ct);
    }

    public async Task<IEnumerable<Unit>> Handle(GetUnitsByProjectIdQuery query, CancellationToken ct = default)
    {
        return await _unitRepository.FindByProjectIdAsync(query.ProjectId, ct);
    }

    public async Task<IEnumerable<Unit>> Handle(GetUnitsByOwnerIdQuery query, CancellationToken ct = default)
    {
        return await _unitRepository.FindByOwnerIdAsync(query.OwnerId, ct);
    }
}
