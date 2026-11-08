using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Repositories;
using IoBuild.Api.Subscriptions.Domain.Services;
using IoBuild.Api.Subscriptions.Domain.Services.Queries;

namespace IoBuild.Api.Subscriptions.Application.Internal.QueryServices;

public class PlanQueryService : IPlanQueryService
{
    private readonly IPlanRepository _planRepository;

    public PlanQueryService(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<IEnumerable<Plan>> Handle(GetAllPlansQuery query, CancellationToken ct = default)
    {
        return await _planRepository.ListAllAsync(ct);
    }

    public async Task<Plan?> Handle(GetPlanByIdQuery query, CancellationToken ct = default)
    {
        return await _planRepository.FindByIdAsync(query.Id, ct);
    }

    public async Task<Plan?> Handle(GetPlanByNameQuery query, CancellationToken ct = default)
    {
        return await _planRepository.FindByNameAsync(query.Name, ct);
    }
}
