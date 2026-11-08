using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Services.Queries;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IUnitQueryService
{
    Task<IEnumerable<Unit>> Handle(GetAllUnitsQuery query, CancellationToken ct = default);
    Task<Unit?> Handle(GetUnitByIdQuery query, CancellationToken ct = default);
    Task<IEnumerable<Unit>> Handle(GetUnitsByProjectIdQuery query, CancellationToken ct = default);
    Task<IEnumerable<Unit>> Handle(GetUnitsByOwnerIdQuery query, CancellationToken ct = default);
}
