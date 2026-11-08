using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Services.Queries;

namespace IoBuild.Api.Subscriptions.Domain.Services;

public interface IPlanQueryService
{
    Task<IEnumerable<Plan>> Handle(GetAllPlansQuery query, CancellationToken ct = default);
    Task<Plan?> Handle(GetPlanByIdQuery query, CancellationToken ct = default);
    Task<Plan?> Handle(GetPlanByNameQuery query, CancellationToken ct = default);
}
