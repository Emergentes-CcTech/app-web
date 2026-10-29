using IoBuild.Api.Subscriptions.Domain.Services.Commands;

namespace IoBuild.Api.Subscriptions.Domain.Services;

public interface IPlanCommandService
{
    Task<int> Handle(CreatePlanCommand command, CancellationToken ct = default);
    Task Handle(UpdatePlanCommand command, CancellationToken ct = default);
}
