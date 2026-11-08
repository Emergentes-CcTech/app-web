using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Repositories;
using IoBuild.Api.Subscriptions.Domain.Services;
using IoBuild.Api.Subscriptions.Domain.Services.Commands;

namespace IoBuild.Api.Subscriptions.Application.Internal.CommandServices;

public class PlanCommandService : IPlanCommandService
{
    private readonly IPlanRepository _planRepository;
    private readonly IoBuildDbContext _dbContext;

    public PlanCommandService(IPlanRepository planRepository, IoBuildDbContext dbContext)
    {
        _planRepository = planRepository;
        _dbContext = dbContext;
    }

    public async Task<int> Handle(CreatePlanCommand command, CancellationToken ct = default)
    {
        var plan = new Plan(command.Name, command.Description, command.Price, command.Interval, command.FeaturesJson);
        await _planRepository.AddAsync(plan, ct);
        await _dbContext.SaveChangesAsync(ct);
        return plan.Id;
    }

    public async Task Handle(UpdatePlanCommand command, CancellationToken ct = default)
    {
        var plan = await _planRepository.FindByIdAsync(command.Id, ct);
        if (plan is null)
        {
            throw new KeyNotFoundException($"Plan with ID {command.Id} not found.");
        }

        plan.Update(command.Name, command.Description, command.Price, command.Interval, command.FeaturesJson);
        await _planRepository.UpdateAsync(plan, ct);
        await _dbContext.SaveChangesAsync(ct);
    }
}
